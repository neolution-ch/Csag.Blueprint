namespace Csag.Blueprint.TestHost.HostedServices;

using Csag.Blueprint.TestHost.Database;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Web.HealthChecks;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Hosted lifecycle service that prepares the database before the application starts accepting
/// requests: creates the schema with <c>EnsureCreated</c> (the Blueprint packages ship no
/// migrations, so the schema comes straight from the EF model), runs the idempotent data seeder,
/// and then flips the readiness gate. A failure stops the application instead of leaving a host
/// running against a broken database.
/// </summary>
public sealed class TestHostStartupService : IHostedLifecycleService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<TestHostStartupService> logger;
    private readonly IHostApplicationLifetime appLifetime;
    private readonly StartupCompletedHealthCheck startupGate;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHostStartupService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to create the seeding scope.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="appLifetime">The application lifetime used to stop the host on startup failure.</param>
    /// <param name="startupGate">The readiness gate flipped once startup completed successfully.</param>
    public TestHostStartupService(
        IServiceProvider serviceProvider,
        ILogger<TestHostStartupService> logger,
        IHostApplicationLifetime appLifetime,
        StartupCompletedHealthCheck startupGate)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.appLifetime = appLifetime;
        this.startupGate = startupGate;
    }

    /// <inheritdoc/>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = this.serviceProvider.CreateScope();

            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
            await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }

            var seeder = scope.ServiceProvider.GetRequiredService<TestHostDataSeeder>();
            await seeder.SeedAsync(cancellationToken);

            this.startupGate.StartupCompleted = true;
            this.logger.LogInformation("TestHost startup completed: schema ensured and seed data in place.");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "TestHost startup failed while creating the schema or seeding data.");
            this.appLifetime.StopApplication();
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
