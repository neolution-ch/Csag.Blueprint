namespace Csag.Blueprint.Testing.UnitTests.Integration;

using Csag.Blueprint.Testing.Integration;

/// <summary>
/// Unit tests for the <see cref="MsSqlTestContainerOrchestrator"/> behavior that must hold without
/// Docker or a reachable SQL Server: side-effect-free construction, safe disposal before start, and
/// the fail-fast guard clauses of <see cref="MsSqlTestContainerOrchestrator.CreateSnapshotAsync"/>
/// and <see cref="MsSqlTestContainerOrchestrator.ResetDatabaseAsync"/>.
/// </summary>
public sealed class MsSqlTestContainerOrchestratorTests
{
    /// <summary>
    /// A syntactically valid SA connection string without an Initial Catalog, pointing at a host
    /// that cannot resolve (.invalid is reserved). Any attempt to open a connection against it
    /// would surface a SqlClient error, so observing <see cref="InvalidOperationException"/> from
    /// the guard clause proves it fired before any network access.
    /// </summary>
    private const string NoCatalogConnectionString =
        "Server=host.invalid,1433;User Id=sa;Password=NotARealPassword123!;TrustServerCertificate=True";

    [Fact]
    public async Task Constructor_WithoutDocker_SucceedsAndDisposesCleanly()
    {
        // Construction must not contact the Docker daemon, and disposing before StartAsync must be
        // a no-op instead of failing on a container that was never built.
        await using var orchestrator = new MsSqlTestContainerOrchestrator();

        orchestrator.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetConnectionString_BeforeStartAsync_Throws()
    {
        await using var orchestrator = new MsSqlTestContainerOrchestrator();

        var exception = Should.Throw<InvalidOperationException>(() => orchestrator.GetConnectionString());

        exception.Message.ShouldContain(nameof(MsSqlTestContainerOrchestrator.StartAsync));
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithoutInitialCatalog_ThrowsBeforeAnyConnectionAttempt()
    {
        await using var orchestrator = new MsSqlTestContainerOrchestrator();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => orchestrator.CreateSnapshotAsync(NoCatalogConnectionString));

        exception.Message.ShouldContain("Initial Catalog");
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithoutInitialCatalog_DoesNotLeakTheConnectionStringOrPassword()
    {
        await using var orchestrator = new MsSqlTestContainerOrchestrator();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => orchestrator.CreateSnapshotAsync(NoCatalogConnectionString));

        exception.Message.ShouldNotContain("NotARealPassword123!");
        exception.Message.ShouldNotContain(NoCatalogConnectionString);
    }

    [Fact]
    public async Task ResetDatabaseAsync_BeforeCreateSnapshotAsync_Throws()
    {
        await using var orchestrator = new MsSqlTestContainerOrchestrator();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => orchestrator.ResetDatabaseAsync());

        exception.Message.ShouldContain(nameof(MsSqlTestContainerOrchestrator.CreateSnapshotAsync));
    }
}
