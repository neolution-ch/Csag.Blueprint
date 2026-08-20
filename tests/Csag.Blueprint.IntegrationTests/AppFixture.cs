namespace Csag.Blueprint.IntegrationTests;

using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.Testing.Integration;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Web.HealthChecks;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Application fixture bootstrapping the TestHost against a real SQL Server Testcontainer.
/// Shared across all test classes in the <see cref="AppFixtureCollection"/>, so one container and
/// one host serve the whole run. The host creates the schema and seeds the deterministic
/// <see cref="SeedData"/> world during startup; this fixture then signs in one client per seeded
/// user, snapshots the database, and restores that snapshot before each test
/// (<see cref="ResetDatabaseAsync"/>). Session tickets live in the host's memory-backed cache, so
/// the pre-authenticated clients survive every database restore.
/// </summary>
public class AppFixture : AppFixture<Program>, IResettableFixture
{
    /// <summary>
    /// Name of the database created on the Testcontainer for the host.
    /// </summary>
    private const string DatabaseName = "CsagBlueprintTestHost";

    private readonly MsSqlTestContainerOrchestrator sqlOrchestrator = new();

    private string connectionString = null!;

    /// <summary>
    /// Gets a pre-authenticated client for <see cref="SeedData.ViewerAEmail"/> (TenantViewer in
    /// tenant A — may read vehicles, not manage them).
    /// </summary>
    public HttpClient ViewerAClient { get; private set; } = null!;

    /// <summary>
    /// Gets a pre-authenticated client for <see cref="SeedData.ManagerAEmail"/> (TenantManager in
    /// tenant A — full vehicle and member management in tenant A).
    /// </summary>
    public HttpClient ManagerAClient { get; private set; } = null!;

    /// <summary>
    /// Gets a pre-authenticated client for <see cref="SeedData.ManagerBEmail"/> (TenantManager in
    /// tenant B — for cross-tenant isolation assertions).
    /// </summary>
    public HttpClient ManagerBClient { get; private set; } = null!;

    /// <summary>
    /// Gets a pre-authenticated client for <see cref="SeedData.PlatformAdminEmail"/> (global
    /// PlatformAdmin — holds only the platform-scope permission, no tenant operational roles).
    /// </summary>
    public HttpClient PlatformClient { get; private set; } = null!;

    /// <summary>
    /// Gets a client without any session, for 401 assertions.
    /// </summary>
    public HttpClient AnonymousClient { get; private set; } = null!;

    /// <summary>
    /// Extracts the XSRF-TOKEN cookie value from a response's Set-Cookie header and sets it as the
    /// X-CSRF-TOKEN default request header on the client, mirroring what a browser frontend does:
    /// read the request-token cookie, echo it as a header on state-changing requests.
    /// </summary>
    /// <param name="client">The HTTP client to configure.</param>
    /// <param name="response">The HTTP response carrying the Set-Cookie header.</param>
    public static void SetCsrfHeaderFromResponse(HttpClient client, HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(response);

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return;
        }

        var cookies = Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList(setCookieHeaders.ToList());
        var xsrfCookie = cookies.FirstOrDefault(c =>
            string.Equals(c.Name.Value, "XSRF-TOKEN", StringComparison.OrdinalIgnoreCase));

        if (xsrfCookie is null)
        {
            return;
        }

        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", xsrfCookie.Value.Value);
    }

    /// <inheritdoc />
    public Task ResetDatabaseAsync() => this.sqlOrchestrator.ResetDatabaseAsync();

    /// <summary>
    /// Opens a <see cref="TestDbContext"/> against the container database for arrange/assert work
    /// outside the HTTP surface. The context carries the tenant and auditable-timestamp save
    /// interceptors, so writes behave like the application's own.
    /// </summary>
    /// <param name="tenantId">
    /// Ambient tenant to set for the scope. Required for touching tenant-owned sets such as
    /// <see cref="TestDbContext.Vehicles"/> — the tenant query filter fails closed, so querying
    /// them with no ambient tenant matches no rows; when passing null, use
    /// <c>IgnoreQueryFilters()</c> deliberately on tenant-owned sets. The ambient value is scoped
    /// to the calling test's async context, so it cannot leak into other tests.
    /// </param>
    /// <returns>A disposable scope owning the context.</returns>
    public TestDbContextScope<TestDbContext> CreateDbContextScope(Guid? tenantId = null)
    {
        if (tenantId.HasValue)
        {
            Csag.Blueprint.Application.Services.TenantContext.SetTenant(tenantId.Value);
        }
        else
        {
            Csag.Blueprint.Application.Services.TenantContext.Clear();
        }

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(this.connectionString)
            .AddInterceptors(new AuditableTimestampInterceptor(), new TenantSaveInterceptor())
            .Options;

        return new TestDbContextScope<TestDbContext>(new TestDbContext(options));
    }

    /// <inheritdoc />
    protected override async ValueTask PreSetupAsync()
    {
        // Set the environment BEFORE the web application factory runs the entry point, so the host
        // loads appsettings.Testing.json (HTTP-friendly cookies, no HTTPS redirect, Swagger on).
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        await this.sqlOrchestrator.StartAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureApp(IWebHostBuilder a)
    {
        // The Testcontainers connection string has no database name; the host's EnsureCreated
        // creates the database on first startup.
        var builder = new SqlConnectionStringBuilder(this.sqlOrchestrator.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        };

        this.connectionString = builder.ConnectionString;
        a.UseSetting("ConnectionStrings:Default", this.connectionString);
    }

    /// <inheritdoc />
    protected override async ValueTask SetupAsync()
    {
        // Fail fast with a clear message if the startup service did not complete. Without this
        // check, a silent host shutdown surfaces as a confusing ObjectDisposedException later.
        var readyCheck = this.Services.GetRequiredService<StartupCompletedHealthCheck>();
        if (!readyCheck.StartupCompleted)
        {
            throw new InvalidOperationException(
                "TestHostStartupService did not complete successfully. "
                + "The host likely failed while creating the schema or seeding data. "
                + "Check the test output for error logs.");
        }

        this.ViewerAClient = await this.CreateAuthenticatedClientAsync(SeedData.ViewerAEmail);
        this.ManagerAClient = await this.CreateAuthenticatedClientAsync(SeedData.ManagerAEmail);
        this.ManagerBClient = await this.CreateAuthenticatedClientAsync(SeedData.ManagerBEmail);
        this.PlatformClient = await this.CreateAuthenticatedClientAsync(SeedData.PlatformAdminEmail);
        this.AnonymousClient = this.CreateClient();

        // Snapshot AFTER the clients signed in, so their tracked session rows are part of the
        // restored state and per-test restores cannot invalidate the shared clients.
        await this.sqlOrchestrator.CreateSnapshotAsync(this.connectionString);
    }

    /// <inheritdoc />
    protected override async ValueTask TearDownAsync()
    {
        this.ViewerAClient?.Dispose();
        this.ManagerAClient?.Dispose();
        this.ManagerBClient?.Dispose();
        this.PlatformClient?.Dispose();
        this.AnonymousClient?.Dispose();

        // Best-effort snapshot drop: container disposal cleans up regardless.
        try
        {
            await this.sqlOrchestrator.DropSnapshotAsync();
        }
        catch (Exception)
        {
            // Intentionally swallowed — the container is removed regardless.
        }

        await this.sqlOrchestrator.DisposeAsync();
    }

    /// <summary>
    /// Signs the given seeded user in through the real login endpoint and returns a client whose
    /// cookie container holds the session and antiforgery cookies and whose default headers carry
    /// the CSRF request token.
    /// </summary>
    /// <param name="email">The seeded user's email; the password is the shared <see cref="SeedData.DefaultPassword"/>.</param>
    /// <returns>An authenticated HTTP client.</returns>
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = this.CreateClient();

        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = email,
                Password = SeedData.DefaultPassword,
            });

        if (!rsp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to authenticate seeded user '{email}': {rsp.StatusCode}");
        }

        SetCsrfHeaderFromResponse(client, rsp);
        return client;
    }
}
