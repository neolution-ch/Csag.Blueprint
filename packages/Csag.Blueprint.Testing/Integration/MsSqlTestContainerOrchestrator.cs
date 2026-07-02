namespace Csag.Blueprint.Testing.Integration;

using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

/// <summary>
/// Manages the full lifecycle of a SQL Server Testcontainer for integration tests,
/// including container start/stop, database snapshot creation, and fast per-test snapshot restores.
/// </summary>
/// <remarks>
/// Intended usage pattern:
/// <list type="number">
///   <item><description>Call <see cref="StartAsync"/> in <c>PreSetupAsync</c>.</description></item>
///   <item><description>Call <c>CreateSnapshotAsync</c> after all seeding is complete (in <c>SetupAsync</c>).</description></item>
///   <item><description>Call <see cref="ResetDatabaseAsync"/> at the start of each test.</description></item>
///   <item><description>Call <see cref="DropSnapshotAsync"/> then <see cref="DisposeAsync"/> in <c>TearDownAsync</c>.</description></item>
/// </list>
/// </remarks>
public sealed class MsSqlTestContainerOrchestrator : IAsyncDisposable
{
    /// <summary>The default SQL Server image used when no image is specified.</summary>
    private const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer container;

    private SqlConnection? masterConnection;
    private string databaseName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsSqlTestContainerOrchestrator"/> class.
    /// </summary>
    /// <param name="image">The SQL Server Docker image to use. Defaults to SQL Server 2022.</param>
    public MsSqlTestContainerOrchestrator(string image = DefaultImage)
    {
        this.container = new MsSqlBuilder(image).Build();
    }

    /// <summary>
    /// Gets the raw Testcontainers connection string (SA credentials, no initial catalog).
    /// Use this as the base for building application-specific connection strings.
    /// </summary>
    /// <returns>The container connection string.</returns>
    public string GetConnectionString() => this.container.GetConnectionString();

    /// <summary>
    /// Starts the SQL Server Testcontainer.
    /// </summary>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync() => this.container.StartAsync();

    /// <summary>
    /// Creates a SQL Server snapshot of the target database for fast subsequent restores.
    /// Opens a persistent connection to <c>master</c> that is reused on every <see cref="ResetDatabaseAsync"/> call.
    /// </summary>
    /// <param name="migrationsConnectionString">
    /// An SA-level connection string pointing at the fully-migrated and seeded database.
    /// The <c>Initial Catalog</c> must be set to the database name to snapshot.
    /// </param>
    /// <returns>A task representing the asynchronous snapshot creation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection string does not specify an <c>Initial Catalog</c>,
    /// or when the database file metadata cannot be read.
    /// </exception>
    public async Task CreateSnapshotAsync(string migrationsConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(migrationsConnectionString);
        this.databaseName = builder.InitialCatalog;

        if (string.IsNullOrEmpty(this.databaseName))
        {
            throw new InvalidOperationException(
                $"Connection string does not specify a database (Initial Catalog): {migrationsConnectionString}");
        }

        var masterConnectionString = new SqlConnectionStringBuilder(migrationsConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        // Open a connection to the application DB to query file info
        await using var dbConnection = new SqlConnection(migrationsConnectionString);
        await dbConnection.OpenAsync();

        await using var masterSetupConnection = new SqlConnection(masterConnectionString);
        await masterSetupConnection.OpenAsync();

        // Drop any stale snapshot from a previous test run
#pragma warning disable CA2100 // Database name is derived from connection string, not from external user input
        using var dropCommand = masterSetupConnection.CreateCommand();
        dropCommand.CommandText = $"DROP DATABASE IF EXISTS [{this.databaseName}_tests]";
        await dropCommand.ExecuteNonQueryAsync();

        // Retrieve logical filename and default data path from the application database
        using var infoCommand = dbConnection.CreateCommand();
        infoCommand.CommandText = @"
            SELECT
                name,
                CAST(SERVERPROPERTY('INSTANCEDEFAULTDATAPATH') AS NVARCHAR(260)) AS DataPath
            FROM sys.database_files
            WHERE type = 0"; // 0 = ROWS (primary data file)

        string logicalName;
        string dataPath;
        await using (var reader = await infoCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Could not read database file information from sys.database_files.");
            }

            logicalName = reader.GetString(0);
            dataPath = reader.GetString(1);
        }

        using var createCommand = masterSetupConnection.CreateCommand();
        createCommand.CommandText = $@"
            CREATE DATABASE [{this.databaseName}_tests] ON
            (NAME = {logicalName}, FILENAME = '{dataPath}{this.databaseName}_tests.ss')
            AS SNAPSHOT OF [{this.databaseName}]";
        await createCommand.ExecuteNonQueryAsync();
#pragma warning restore CA2100

        Console.WriteLine($"[MsSqlTestContainerOrchestrator] Created database snapshot: {this.databaseName}_tests");

        // Keep a persistent master connection open for ultra-fast per-test restores.
        // Reusing one connection avoids TCP handshake and authentication overhead on every call.
        this.masterConnection = new SqlConnection(masterConnectionString);
        await this.masterConnection.OpenAsync();
    }

    /// <summary>
    /// Restores the database from its snapshot, returning it to the post-seeding state.
    /// Clears all ADO.NET connection pools first to prevent SINGLE_USER from blocking.
    /// </summary>
    /// <returns>A task representing the asynchronous restore operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CreateSnapshotAsync"/> has not been called successfully before this method.
    /// </exception>
#pragma warning disable CA2100 // Database name is derived from connection string, not from external user input
    public async Task ResetDatabaseAsync()
    {
        if (this.masterConnection is null || string.IsNullOrEmpty(this.databaseName))
        {
            throw new InvalidOperationException($"{nameof(this.ResetDatabaseAsync)} cannot be called before {nameof(this.CreateSnapshotAsync)} has completed successfully.");
        }

        // Drop all idle ADO.NET pooled connections so SINGLE_USER completes instantly.
        // Without this, SQL Server must terminate each pooled connection one-by-one (~3 seconds).
        SqlConnection.ClearAllPools();

        var query = $@"
            ALTER DATABASE [{this.databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            RESTORE DATABASE [{this.databaseName}] FROM DATABASE_SNAPSHOT = '{this.databaseName}_tests';
            ALTER DATABASE [{this.databaseName}] SET MULTI_USER;";

        using var command = this.masterConnection.CreateCommand();
        command.CommandText = query;
        await command.ExecuteNonQueryAsync();
    }
#pragma warning restore CA2100

    /// <summary>
    /// Drops the snapshot database and releases the persistent master connection.
    /// Call this during teardown before <see cref="DisposeAsync"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous cleanup.</returns>
    public async Task DropSnapshotAsync()
    {
        if (this.masterConnection is null)
        {
            return;
        }

#pragma warning disable CA2100 // Database name is derived from connection string, not from external user input
        using var command = this.masterConnection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS [{this.databaseName}_tests]";
        await command.ExecuteNonQueryAsync();
#pragma warning restore CA2100

        await this.masterConnection.DisposeAsync();
        this.masterConnection = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Guard: dispose persistent connection if DropSnapshotAsync was not called
        if (this.masterConnection is not null)
        {
            await this.masterConnection.DisposeAsync();
            this.masterConnection = null;
        }

        await this.container.DisposeAsync();
    }
}
