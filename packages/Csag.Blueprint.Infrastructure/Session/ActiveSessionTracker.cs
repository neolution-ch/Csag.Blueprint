namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IActiveSessionTracker"/> that writes the tracked session row through
/// the pooled context factory, so it is safe to consume from singletons such as the ticket store.
/// </summary>
/// <typeparam name="TContext">The application database context type.</typeparam>
public sealed class ActiveSessionTracker<TContext> : IActiveSessionTracker
    where TContext : DbContext
{
    // Mirror the mapped column lengths in BlueprintActiveSessionConfiguration so client-supplied values are
    // clamped rather than allowed to fail the write with a SQL truncation error.
    private const int UserAgentMaxLength = 500;
    private const int IpAddressMaxLength = 50;

    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveSessionTracker{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory used to write active session records.</param>
    /// <param name="timeProvider">The clock that stamps when a session was created.</param>
    public ActiveSessionTracker(IDbContextFactory<TContext> dbContextFactory, TimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public Task TrackAsync(SessionTrackingEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The session key is the value every later lookup keys off, so it is rejected rather than clamped like the
        // diagnostic fields: a shortened key would be stored under a value the cookie's ticket key no longer
        // matches, leaving a row that can never be revoked or refreshed by key. A blank key is refused because the
        // cache drops a null/empty/whitespace key and reads and writes the shared CacheId.AuthTicket slot instead,
        // where a claims refresh would store one user's ticket for the next blank-keyed session to pick up. An
        // over-budget key is refused because the cache throws on it: the row would commit while its ticket could
        // never be read, rewritten or removed, and such a row also aborts revocation and refresh for the user's
        // other sessions once the loop reaches it.
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SessionKey);

        if (SessionValueGuards.ExceedsCacheKeyBudget(entry.SessionKey, SessionConstants.SessionKeyMaxCacheKeyBytes))
        {
            throw new ArgumentException(
                $"Session key must be at most {SessionConstants.SessionKeyMaxCacheKeyBytes} bytes once URL-encoded",
                $"{nameof(entry)}.{nameof(entry.SessionKey)}");
        }

        // The guards sit in this non-async wrapper so a rejected key surfaces at the call site rather than on a
        // task the caller might not await.
        return this.TrackCoreAsync(Clamp(entry), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> RenewAsync(SessionTrackingEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return this.RenewCoreAsync(Clamp(entry), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> UntrackAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rowsAffected = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    private static SessionTrackingEntry Clamp(SessionTrackingEntry entry)
    {
        // The user agent is a client-supplied header bounded only by the server's header limits, and both values
        // are only ever displayed, so neither may fail the write that keeps a session revocable.
        return entry with
        {
            UserAgent = SessionValueGuards.Truncate(entry.UserAgent, UserAgentMaxLength),
            IpAddress = SessionValueGuards.Truncate(entry.IpAddress, IpAddressMaxLength),
        };
    }

    private async Task<bool> RenewCoreAsync(SessionTrackingEntry entry, CancellationToken cancellationToken)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await this.UpdateRowAsync(dbContext, entry, cancellationToken);
    }

    private async Task TrackCoreAsync(SessionTrackingEntry entry, CancellationToken cancellationToken)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // A sign-in over a live session cookie reuses that cookie's key, so a row may already exist under it, and
        // the unique index on SessionKey rules out a blind insert. Updating first keeps the common case — a fresh
        // key — to one empty update and an insert, without a failed insert logged on every sign-in over a cookie.
        if (await this.UpdateRowAsync(dbContext, entry, cancellationToken))
        {
            return;
        }

        // Id is intentionally left unset so the NEWSEQUENTIALID() store default configured in
        // BlueprintActiveSessionConfiguration applies, keeping the clustered primary key monotonic on this
        // insert-heavy table (assigning a random Guid here would defeat it).
        dbContext.Set<BlueprintActiveSession>().Add(new BlueprintActiveSession
        {
            UserId = entry.UserId,
            SessionKey = entry.SessionKey,
            CreatedAt = this.timeProvider.GetUtcNow(),
            ExpiresAt = entry.ExpiresAt,
            UserAgent = entry.UserAgent,
            IpAddress = entry.IpAddress,
            CurrentTenantId = entry.TenantId,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two sign-ins over the same cookie can both find no row and race to insert one. The loser updates the
            // winner's row instead, so the row ends up describing whichever ticket was written last.
            dbContext.ChangeTracker.Clear();
            if (!await this.UpdateRowAsync(dbContext, entry, cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task<bool> UpdateRowAsync(TContext dbContext, SessionTrackingEntry entry, CancellationToken cancellationToken)
    {
        var userId = entry.UserId;
        var userAgent = entry.UserAgent;
        var ipAddress = entry.IpAddress;
        var now = this.timeProvider.GetUtcNow();

        // A single statement, so the row can never be observed half re-attributed. SQL evaluates every assigned
        // expression against the row as it was before the update, so the conditional columns compare against the
        // previous account even though UserId is assigned in the same statement.
        var rowsAffected = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == entry.SessionKey)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.CreatedAt, s => s.UserId == userId ? s.CreatedAt : now)
                    .SetProperty(s => s.UserAgent, s => s.UserId == userId ? s.UserAgent : userAgent)
                    .SetProperty(s => s.IpAddress, s => s.UserId == userId ? s.IpAddress : ipAddress)
                    .SetProperty(s => s.UserId, userId)
                    .SetProperty(s => s.ExpiresAt, entry.ExpiresAt)
                    .SetProperty(s => s.CurrentTenantId, entry.TenantId),
                cancellationToken);

        return rowsAffected > 0;
    }
}
