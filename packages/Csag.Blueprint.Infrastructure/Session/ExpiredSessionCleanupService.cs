namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Deletes the tracking rows of expired sessions — user sessions (<see cref="BlueprintActiveSession"/>) and
/// service-account sessions (<see cref="BlueprintServiceAccountSession"/>) alike — on a fixed interval (see
/// <see cref="BlueprintSessionOptions.ExpiredSessionCleanup"/>). Their cached tickets and markers expire with them, so
/// the rows are all that is left of those sessions; listings and token validation already ignore them, so this
/// reclaims storage rather than changing what anyone can do.
/// </summary>
/// <remarks>
/// Every replica of the application runs the job. Each pass is a series of set-based deletes of rows that expired
/// before the pass began, so passes that overlap on different replicas delete overlapping rows without either
/// failing, and need no coordination.
/// </remarks>
/// <typeparam name="TContext">The application database context type.</typeparam>
public sealed class ExpiredSessionCleanupService<TContext> : BackgroundService
    where TContext : DbContext
{
    // SQL Server escalates a statement's row locks to a table lock at around 5,000 locks. Deleting in smaller
    // batches keeps every statement on row locks, so a pass never blocks the sign-ins and renewals writing the same
    // table, however many expired rows have piled up.
    private const int BatchSize = 1000;

    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly IOptions<BlueprintSessionOptions> sessionOptions;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<ExpiredSessionCleanupService<TContext>> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiredSessionCleanupService{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory used to delete the rows.</param>
    /// <param name="sessionOptions">The session settings carrying the job's switch and interval.</param>
    /// <param name="timeProvider">The clock that decides what has expired and drives the interval.</param>
    /// <param name="logger">The logger.</param>
    public ExpiredSessionCleanupService(
        IDbContextFactory<TContext> dbContextFactory,
        IOptions<BlueprintSessionOptions> sessionOptions,
        TimeProvider timeProvider,
        ILogger<ExpiredSessionCleanupService<TContext>> logger)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs one pass: deletes every user and service-account tracking row whose session expired at or before the
    /// current instant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows deleted, across both tables.</returns>
    public async Task<int> RemoveExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        // One cutoff for the whole pass, so the loops end even while sessions keep expiring underneath them.
        var cutoff = this.timeProvider.GetUtcNow();

        var removed = await this.RemoveInBatchesAsync<BlueprintActiveSession>(
            sessions => sessions.Where(s => s.ExpiresAt <= cutoff).OrderBy(s => s.ExpiresAt),
            cancellationToken);

        removed += await this.RemoveInBatchesAsync<BlueprintServiceAccountSession>(
            sessions => sessions.Where(s => s.ExpiresAt <= cutoff).OrderBy(s => s.ExpiresAt),
            cancellationToken);

        return removed;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanup = this.sessionOptions.Value.ExpiredSessionCleanup;
        if (!cleanup.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(cleanup.Interval, this.timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await this.RunPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task<int> RemoveInBatchesAsync<TSession>(
        Func<IQueryable<TSession>, IQueryable<TSession>> expired,
        CancellationToken cancellationToken)
        where TSession : class
    {
        var removed = 0;
        int batch;

        do
        {
            await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);
            batch = await expired(dbContext.Set<TSession>())
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            removed += batch;
        }
        while (batch == BatchSize);

        return removed;
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            var removed = await this.RemoveExpiredSessionsAsync(stoppingToken);
            if (removed > 0)
            {
                this.logger.LogInformation("Removed {Count} expired session tracking rows", removed);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // An exception escaping a hosted service stops the host by default. A failed pass only delays the
            // cleanup, so the job logs it and tries again on the next tick.
            this.logger.LogWarning(ex, "Removing expired session tracking rows failed; the next pass retries");
        }
    }
}
