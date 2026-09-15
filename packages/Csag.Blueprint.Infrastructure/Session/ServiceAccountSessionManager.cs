namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Generic implementation of <see cref="IServiceAccountSessionManager"/>. Tracks service-account sessions
/// as a distributed-cache marker (the fast-path reference-token backing store, reusing the same
/// <see cref="IDistributedCache{CacheId}"/> as user sessions) plus a database tracking row, and resolves
/// each request's authorization from the account's CURRENT database state.
/// </summary>
/// <typeparam name="TContext">The application database context type.</typeparam>
public sealed class ServiceAccountSessionManager<TContext> : IServiceAccountSessionManager
    where TContext : DbContext
{
    // The distributed cache composes its key as "CacheId:ServiceAccountSession_" + Uri.EscapeDataString(key) and
    // rejects a generated key over 250 UTF-8 bytes, so the 30-byte prefix leaves 220 for the encoded key.
    // Percent-encoding never shrinks a string, so a key inside this budget also fits the 500-character SessionKey
    // column: the cache is the binding limit of the two. The prefix assumes the cache's default key options —
    // an application that configures an environment prefix or a schema version lengthens the generated key and
    // lowers its own effective ceiling below this one. Issuance writes its marker after committing the row, so
    // that residual gap is compensated rather than prevented: TrackSessionCoreAsync deletes the row it just
    // committed when the marker write is refused.
    private const int SessionKeyMaxCacheKeyBytes = 220;

    // Mirror the mapped column lengths in BlueprintServiceAccountSessionConfiguration so client-supplied values
    // are clamped rather than allowed to fail the insert with a SQL truncation error.
    private const int UserAgentMaxLength = 500;
    private const int IpAddressMaxLength = 50;

    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly IDistributedCache<CacheId> cache;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountSessionManager{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory used to persist and query session records.</param>
    /// <param name="cache">The strongly-typed distributed cache used for the fast-path session marker.</param>
    /// <param name="timeProvider">The clock used to stamp session creation times and to evaluate session expiry.</param>
    public ServiceAccountSessionManager(
        IDbContextFactory<TContext> dbContextFactory,
        IDistributedCache<CacheId> cache,
        TimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public Task TrackSessionAsync(
        Guid serviceAccountId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        // The session key is the value every later lookup keys off, so it is rejected rather than clamped like
        // the diagnostic fields below: a shortened key would be stored under a value the token's session-id claim
        // no longer matches, leaving a row that can never be validated or revoked by key. A blank key is refused
        // because the cache drops a null/empty/whitespace key and writes the marker to the shared
        // CacheId.ServiceAccountSession slot, where every other blank-keyed session would read it back and
        // resolve to the wrong account. Both checks run before any I/O because this method is not transactional:
        // the marker is written only after SaveChangesAsync has committed, so a key the cache refuses would
        // otherwise strand a committed row whose marker can never be written, read, or removed — and a row
        // stranded that way also aborts RevokeServiceAccountSessionsAsync for every other session on the account.
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);

        if (SessionValueGuards.ExceedsCacheKeyBudget(sessionKey, SessionKeyMaxCacheKeyBytes))
        {
            throw new ArgumentException($"Session key must be at most {SessionKeyMaxCacheKeyBytes} bytes once URL-encoded", nameof(sessionKey));
        }

        // The guards sit in this non-async wrapper so they surface at the call site rather than on the awaited task.
        return this.TrackSessionCoreAsync(serviceAccountId, sessionKey, expiresAt, userAgent, ipAddress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServiceAccountSessionValidation?> ValidateSessionAsync(string? sessionKey, CancellationToken cancellationToken = default)
    {
        // A key outside the bounds TrackSessionAsync enforces cannot belong to a tracked session, so here it
        // simply means "no such session". The session-id claim reaching this method is client-supplied, so it is
        // filtered rather than passed to the cache: the cache throws on an over-long key, which would turn a
        // rejected request into an unhandled exception in the authentication pipeline, and silently redirects a
        // blank one to the shared CacheId.ServiceAccountSession slot.
        if (!SessionValueGuards.IsWellFormedSessionKey(sessionKey, SessionKeyMaxCacheKeyBytes))
        {
            return null;
        }

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1) Resolve the session to its owning service account. Fast path: the cache marker. If the marker is
        //    missing (cache eviction/flush, or the entry was removed by revocation), fall back to the tracking
        //    row. We deliberately do NOT re-prime the cache from the fallback: revocation sweeps the marker both
        //    before and after deleting the row, but a re-prime landing after that final sweep would write a marker
        //    outliving the row it was read from, resurrecting a revoked session — and no later revoke could clear
        //    it, because revocation enumerates rows. Only issuance writes markers, which is what lets
        //    TrackSessionCoreAsync be responsible for confirming its own row still exists. Falling through to the
        //    DB on every request after a cache flush is a bounded performance cost, not a correctness problem.
        var serviceAccountId = await this.ResolveServiceAccountIdAsync(dbContext, sessionKey, cancellationToken);
        if (serviceAccountId is null)
        {
            return null;
        }

        // 2) Load the account's CURRENT state. IgnoreQueryFilters because there is no ambient tenant context
        //    yet (authentication runs before TenantMiddleware) and the tenant filter fails closed otherwise.
        var account = await dbContext.Set<BlueprintServiceAccount>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(sa => sa.Id == serviceAccountId.Value, cancellationToken);

        // Missing (deleted) or deactivated accounts reject immediately — this is what makes deactivation and
        // role/permission changes take effect per request rather than being frozen in the token.
        if (account is null || !account.IsActive)
        {
            return null;
        }

        return new ServiceAccountSessionValidation
        {
            ServiceAccountId = account.Id,
            TenantId = account.TenantId,
            Roles = account.Roles.ToList(),
            Permissions = account.Permissions.ToList(),
        };
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        // Same rule as ValidateSessionAsync: a key no tracked session can carry means "nothing to revoke".
        // Filtering it here also keeps a blank key away from the removals below, which would otherwise clear the
        // shared CacheId.ServiceAccountSession slot.
        if (!SessionValueGuards.IsWellFormedSessionKey(sessionKey, SessionKeyMaxCacheKeyBytes))
        {
            return false;
        }

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Snapshot the id AND key of the matching rows and delete exactly that set by id, the shape
        // RevokeServiceAccountSessionsAsync uses. The two stores do not agree on what makes two keys equal: the
        // cache compares the key it is handed, while this predicate becomes SQL string equality, which folds case
        // under a case-insensitive column collation and ignores trailing spaces under every collation SQL Server
        // has. Removing the marker under the caller's spelling alone would then delete the row a differently
        // spelled key stored and leave that key's marker live — authorizing the revoked session until it expires,
        // and unreachable by any later revoke, because revocation enumerates rows. The caller's own key stays in
        // the sweep set so a marker with no row is still removable by the key the caller holds.
        var tracked = await dbContext.Set<BlueprintServiceAccountSession>()
            .AsNoTracking()
            .Where(s => s.SessionKey == sessionKey)
            .Select(s => new { s.Id, s.SessionKey })
            .ToListAsync(cancellationToken);

        var markerKeys = tracked.Select(s => s.SessionKey)
            .Append(sessionKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Full revocation requires removing BOTH the cache marker (closes the validation fast path) AND the
        // tracking row (closes the DB fallback in ResolveServiceAccountIdAsync) — while either remains, a
        // still-active account's token keeps authorizing until it expires. The marker is removed FIRST only so a
        // partial failure leaves the row behind as a durable, listable, retry-clearable record; the reverse order
        // could delete the row while the marker survives, orphaning a live marker no later revoke can find (they
        // key off rows) until it expires.
        foreach (var markerKey in markerKeys)
        {
            await this.RemoveMarkerIgnoringKeyRefusalAsync(markerKey, cancellationToken);
        }

        var trackedIds = tracked.Select(s => s.Id).ToList();
        var deleted = await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => trackedIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Sweep the markers again now the rows are gone. Issuance commits its row and only then writes its marker,
        // so a token issued concurrently can slot a marker in between the removals above and this delete — a marker
        // that would outlive its row and keep authorizing the revoked session. The removal is idempotent, so this
        // costs one no-op cache delete in the ordinary case.
        foreach (var markerKey in markerKeys)
        {
            await this.RemoveMarkerIgnoringKeyRefusalAsync(markerKey, cancellationToken);
        }

        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task<int> RevokeServiceAccountSessionsAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Snapshot the id AND key of every matching row, then delete exactly that snapshotted set (NOT a
        // re-evaluated ServiceAccountId filter). This guarantees the rows deleted and the markers removed are the
        // same set even if a token is issued for this account concurrently: such a session is simply absent from
        // the snapshot and keeps BOTH its row and marker, instead of having its row deleted while its marker is
        // orphaned. The snapshot-then-delete shape follows SessionManager.RevokeSessionsCoreAsync; the marker and
        // row are removed in the opposite order to that method, for the reason given below.
        var sessions = await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => s.ServiceAccountId == serviceAccountId)
            .Select(s => new { s.Id, s.SessionKey })
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return 0;
        }

        // Remove the authoritative fast-path markers BEFORE deleting the rows. If a removal fails the exception
        // propagates and the rows are left intact, so a retry re-derives the same set and clears the remaining
        // markers (removing an already-gone marker is a no-op). The reverse order could delete a row while its
        // marker survives, leaving a live marker no subsequent revoke can find until it expires.
        //
        // A key the cache will not compose is the exception to that retry logic rather than a case for it: no
        // retry can make it addressable, so propagating would leave every row on the account undeleted and
        // still resolvable through the database fallback. Since this is the path secret rotation, deactivation
        // and deletion all take, the refusal is treated as a missing marker and the durable rows still go.
        foreach (var session in sessions)
        {
            await this.RemoveMarkerIgnoringKeyRefusalAsync(session.SessionKey, cancellationToken);
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var revoked = await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Sweep the markers again now the rows are gone, for the reason given in RevokeSessionAsync: a session
        // already present in the snapshot can have its marker written between the sweep above and this delete,
        // because issuance commits its row before writing its marker. The snapshot keeps the deleted rows and the
        // swept markers in step; it does not order this method against an issuance already in flight.
        foreach (var session in sessions)
        {
            await this.RemoveMarkerIgnoringKeyRefusalAsync(session.SessionKey, cancellationToken);
        }

        return revoked;
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = this.timeProvider.GetUtcNow();

        // Only the tracking table is cleaned here; the distributed-cache markers carry their own absolute expiry.
        return await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => s.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a tracking row whose marker could not be written, so a failed issuance leaves nothing behind.
    /// </summary>
    /// <remarks>
    /// Runs uncancellable: the cache failure this compensates for may itself have been a cancellation, and the
    /// row still has to come out. A failure here is swallowed so the caller sees the original cache exception,
    /// which is the one that explains why issuance failed — the row is then left for the opportunistic reap on
    /// this account's next issuance, or for <c>CleanupExpiredSessionsAsync</c>.
    /// </remarks>
    /// <param name="dbContext">The context the row was committed through.</param>
    /// <param name="sessionKey">The session key identifying the row to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RemoveTrackingRowAsync(TContext dbContext, string sessionKey)
    {
        try
        {
            await dbContext.Set<BlueprintServiceAccountSession>()
                .Where(s => s.SessionKey == sessionKey)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Deliberately ignored; see the remarks above.
        }
    }

    /// <summary>
    /// Removes a fast-path marker, treating the cache's refusal to compose the key as "no such marker".
    /// </summary>
    /// <remarks>
    /// Only a caller-supplied key can be refused here. The budget <c>IsWellFormedSessionKey</c> enforces is
    /// measured against the cache's default key options, so an application configuring an environment prefix or a
    /// schema version lowers its own effective ceiling below it, and the abstraction exposes no way to read
    /// either setting back. A key the cache will not compose is one it also refused at issuance, so no marker can
    /// exist under it and revocation has nothing to remove. Every other failure propagates, which is what leaves
    /// the tracking row in place as the durable, retry-clearable record of the session.
    /// </remarks>
    /// <param name="sessionKey">The session key identifying the marker to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RemoveMarkerIgnoringKeyRefusalAsync(string sessionKey, CancellationToken cancellationToken)
    {
        try
        {
            await this.cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, cancellationToken);
        }
        catch (ArgumentException)
        {
            // Deliberately ignored; see the remarks above.
        }
    }

    /// <summary>
    /// Removes the fast-path marker for a session whose issuance is failing, so no marker is left describing a
    /// tracking row that is about to be deleted or is already gone.
    /// </summary>
    /// <remarks>
    /// Runs uncancellable and swallows its own failure for the same reasons as
    /// <see cref="RemoveTrackingRowAsync"/>: the cache failure being compensated may itself have been a
    /// cancellation, and the caller must still see the original exception, which is the one that explains why
    /// issuance failed. A key the cache refused to compose is refused for a removal exactly as it was for the
    /// write, so this throwing is the expected outcome whenever that is what brought issuance here.
    /// </remarks>
    /// <param name="sessionKey">The session key identifying the marker to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RemoveCacheMarkerAsync(string sessionKey)
    {
        try
        {
            await this.cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, CancellationToken.None);
        }
        catch (Exception)
        {
            // Deliberately ignored; see the remarks above.
        }
    }

    private async Task TrackSessionCoreAsync(
        Guid serviceAccountId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = this.timeProvider.GetUtcNow();

        // Id is intentionally left unset so the NEWSEQUENTIALID() store default applies, keeping the clustered
        // primary key monotonic on this insert-heavy table (assigning a random Guid here would defeat it).
        dbContext.Set<BlueprintServiceAccountSession>().Add(new BlueprintServiceAccountSession
        {
            ServiceAccountId = serviceAccountId,
            SessionKey = sessionKey,
            CreatedAt = now,
            ExpiresAt = expiresAt,

            // Clamp to the mapped column lengths so a client-supplied over-length User-Agent (or IP) cannot turn
            // token issuance into a SQL truncation error.
            UserAgent = SessionValueGuards.Truncate(userAgent, UserAgentMaxLength),
            IpAddress = SessionValueGuards.Truncate(ipAddress, IpAddressMaxLength),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Opportunistically reap this account's already-expired tracking rows on the same write. This keeps the
        // table bounded per active account WITHOUT a background job (which would be unreliable on scale-to-zero,
        // multi-instance hosting): whichever instance issues a token performs the cleanup, seeking on the
        // ServiceAccountId index. Expired rows are inert for authorization (ResolveServiceAccountIdAsync filters
        // ExpiresAt > now); this only stops them accumulating.
        try
        {
            await dbContext.Set<BlueprintServiceAccountSession>()
                .Where(s => s.ServiceAccountId == serviceAccountId && s.ExpiresAt <= now)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Housekeeping for rows this session does not own must not decide whether this session is issued.
            // The row above is already committed and the compensation below covers only the marker write, so a
            // failure propagating from here would abandon that row with no marker and nothing to take it back
            // out. Unreaped rows stay inert, and this account's next issuance tries again.
        }

        // Prime the fast-path marker so subsequent requests validate the session existence without a DB read.
        // The absolute expiry matches the token/session lifetime so the marker self-cleans.
        try
        {
            await this.SetCacheMarkerAsync(sessionKey, serviceAccountId, expiresAt, cancellationToken);
        }
        catch (Exception)
        {
            // The budget TrackSessionAsync enforces is measured against the cache's DEFAULT key options, so an
            // application that configures an environment prefix or a schema version has a lower effective
            // ceiling and this write can still be refused for a key that passed the guard. The row is committed
            // by then, and a row the cache cannot address is worse than no session at all: it can never be
            // validated through the fast path, and it aborts RevokeServiceAccountSessionsAsync for every other
            // session on the account, because that method feeds each snapshotted key back to the cache. Take the
            // row out again so issuance fails having written nothing.
            //
            // Drop the marker first, and unconditionally. A write that throws has not necessarily been refused:
            // a timeout or a cancellation can surface after the backend already accepted the entry, and a marker
            // that survives the row deleted below is a live fast-path entry no revoke can reach, because
            // revocation enumerates rows. Removing it before the row also keeps the ordering the revocation paths
            // use, so the two never race in opposite directions.
            await this.RemoveCacheMarkerAsync(sessionKey);
            await RemoveTrackingRowAsync(dbContext, sessionKey);
            throw;
        }

        // Issuance is not atomic: the row committed above and the marker just written are two separate steps, and
        // a revocation can run to completion between them. Its marker removal finds nothing, its row delete
        // succeeds, and the marker written here is left describing a row that no longer exists — validating a
        // revoked token on the fast path, and unreachable by any later revoke because revocation enumerates rows.
        // Confirm the row is still there and fail closed by dropping the marker if it is not. Revocation sweeps
        // the marker again after deleting the row, which closes the mirror interleaving where this check reads the
        // row before that delete lands.
        //
        // Uncancellable, like the compensation above: from the marker write onwards the marker is live, so
        // abandoning this check on a cancelled request is what leaves the orphan it exists to prevent.
        var stillTracked = await dbContext.Set<BlueprintServiceAccountSession>()
            .AsNoTracking()
            .AnyAsync(s => s.SessionKey == sessionKey, CancellationToken.None);

        if (!stillTracked)
        {
            await this.RemoveCacheMarkerAsync(sessionKey);
        }
    }

    private async Task<Guid?> ResolveServiceAccountIdAsync(TContext dbContext, string sessionKey, CancellationToken cancellationToken)
    {
        string? cached;
        try
        {
            cached = await this.cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, cancellationToken);
        }
        catch (ArgumentException)
        {
            // The cache composes and length-checks its generated key inside the call, so a key inside the budget
            // this manager enforces can still be refused: that budget is measured against the cache's default key
            // options, and an application configuring an environment prefix or a schema version lowers its own
            // effective ceiling below it — with no way to read either setting back through the abstraction. The
            // key reaching this method comes from a token's session-id claim, so the refusal must not become an
            // unhandled exception in the authentication pipeline. It is a definitive "no such session": the same
            // composition refused the write that would have stored an entry, so no marker under this key exists,
            // and the fallback below still gets its chance on the tracking row.
            cached = null;
        }

        if (!string.IsNullOrEmpty(cached) && Guid.TryParse(cached, CultureInfo.InvariantCulture, out var cachedId))
        {
            return cachedId;
        }

        // Fallback for a lost cache marker: honor the tracking row only while it is present and unexpired.
        // A revoked session has no row, so this correctly returns null. No re-prime (see ValidateSessionAsync).
        //
        // The predicate becomes SQL string equality, which folds case under a case-insensitive column collation
        // and ignores trailing spaces under every SQL Server collation, so it can match a row whose key is not
        // the one the token carries. The session key is the whole of the token's binding to a session, so the
        // candidates are re-compared ordinally here — the way the cache compares them, and the way the marker
        // path above already does implicitly. Filtering in memory rather than with EF.Functions.Collate keeps
        // this provider-neutral; the unique index means the candidate set is a row or two.
        var now = this.timeProvider.GetUtcNow();
        var candidates = await dbContext.Set<BlueprintServiceAccountSession>()
            .AsNoTracking()
            .Where(s => s.SessionKey == sessionKey && s.ExpiresAt > now)
            .Select(s => new { s.SessionKey, s.ServiceAccountId })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(s => string.Equals(s.SessionKey, sessionKey, StringComparison.Ordinal))
            .Select(s => (Guid?)s.ServiceAccountId)
            .FirstOrDefault();
    }

    private async Task SetCacheMarkerAsync(string sessionKey, Guid serviceAccountId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var cacheOptions = new CacheEntryOptions
        {
            AbsoluteExpiration = expiresAt,
        };

        await this.cache.SetWithOptionsAsync(
            CacheId.ServiceAccountSession,
            sessionKey,
            serviceAccountId.ToString("D", CultureInfo.InvariantCulture),
            cacheOptions,
            cancellationToken);
    }
}
