namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using System.Text;
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
    // lowers its own effective ceiling below this one.
    private const int SessionKeyMaxCacheKeyBytes = 220;

    // Mirror the mapped column lengths in BlueprintServiceAccountSessionConfiguration so client-supplied values
    // are clamped rather than allowed to fail the insert with a SQL truncation error.
    private const int UserAgentMaxLength = 500;
    private const int IpAddressMaxLength = 50;

    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly IDistributedCache<CacheId> cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountSessionManager{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory used to persist and query session records.</param>
    /// <param name="cache">The strongly-typed distributed cache used for the fast-path session marker.</param>
    public ServiceAccountSessionManager(IDbContextFactory<TContext> dbContextFactory, IDistributedCache<CacheId> cache)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

        if (ExceedsCacheKeyBudget(sessionKey))
        {
            throw new ArgumentException($"Session key must be at most {SessionKeyMaxCacheKeyBytes} bytes once URL-encoded", nameof(sessionKey));
        }

        // The guards sit in this non-async wrapper so they surface at the call site rather than on the awaited task.
        return this.TrackSessionCoreAsync(serviceAccountId, sessionKey, expiresAt, userAgent, ipAddress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServiceAccountSessionValidation?> ValidateSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        if (!IsWellFormedSessionKey(sessionKey))
        {
            return null;
        }

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1) Resolve the session to its owning service account. Fast path: the cache marker. If the marker is
        //    missing (cache eviction/flush, or the entry was removed by revocation), fall back to the tracking
        //    row. We deliberately do NOT re-prime the cache from the fallback: revocation removes the marker
        //    before deleting the row, so a fallback that landed in that window would write a marker outliving
        //    the row it was read from, resurrecting a revoked session — and no later revoke could clear it,
        //    because revocation enumerates rows. Falling through to the DB on every request after a cache
        //    flush is a bounded performance cost, not a correctness problem.
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
        if (!IsWellFormedSessionKey(sessionKey))
        {
            return false;
        }

        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Full revocation requires removing BOTH the cache marker (closes the validation fast path) AND the
        // tracking row (closes the DB fallback in ResolveServiceAccountIdAsync) — while either remains, a
        // still-active account's token keeps authorizing until it expires. The marker is removed FIRST only so a
        // partial failure leaves the row behind as a durable, listable, retry-clearable record; the reverse order
        // could delete the row while the marker survives, orphaning a live marker no later revoke can find (they
        // key off rows) until it expires.
        await this.cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, cancellationToken);

        var deleted = await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteDeleteAsync(cancellationToken);

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
        foreach (var session in sessions)
        {
            await this.cache.RemoveAsync(CacheId.ServiceAccountSession, session.SessionKey, cancellationToken);
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var revoked = await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        return revoked;
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Only the tracking table is cleaned here; the distributed-cache markers carry their own absolute expiry.
        return await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => s.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    // Measured the way the cache measures it rather than by character count: the key is percent-encoded before
    // the cache checks its length, so every character outside the URI unreserved set costs three bytes (twelve
    // for a non-BMP character) and a 220-character standard-base64 key is already over budget.
    private static bool ExceedsCacheKeyBudget(string sessionKey)
        => Encoding.UTF8.GetByteCount(Uri.EscapeDataString(sessionKey)) > SessionKeyMaxCacheKeyBytes;

    // A key outside the bounds TrackSessionAsync enforces cannot belong to a tracked session, so to the read
    // paths it simply means "no such session". They filter on it rather than passing it to the cache because the
    // session-id claim reaching them is client-supplied: the cache throws on an over-long key, which would turn a
    // rejected request into an unhandled exception in the authentication pipeline, and silently redirects a blank
    // one to the shared CacheId.ServiceAccountSession slot.
    private static bool IsWellFormedSessionKey(string sessionKey)
        => !string.IsNullOrWhiteSpace(sessionKey) && !ExceedsCacheKeyBudget(sessionKey);

    private async Task TrackSessionCoreAsync(
        Guid serviceAccountId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Id is intentionally left unset so the NEWSEQUENTIALID() store default applies, keeping the clustered
        // primary key monotonic on this insert-heavy table (assigning a random Guid here would defeat it).
        dbContext.Set<BlueprintServiceAccountSession>().Add(new BlueprintServiceAccountSession
        {
            ServiceAccountId = serviceAccountId,
            SessionKey = sessionKey,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,

            // Clamp to the mapped column lengths so a client-supplied over-length User-Agent (or IP) cannot turn
            // token issuance into a SQL truncation error.
            UserAgent = Truncate(userAgent, UserAgentMaxLength),
            IpAddress = Truncate(ipAddress, IpAddressMaxLength),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Opportunistically reap this account's already-expired tracking rows on the same write. This keeps the
        // table bounded per active account WITHOUT a background job (which would be unreliable on scale-to-zero,
        // multi-instance hosting): whichever instance issues a token performs the cleanup, seeking on the
        // ServiceAccountId index. Expired rows are inert for authorization (ResolveServiceAccountIdAsync filters
        // ExpiresAt > now); this only stops them accumulating.
        await dbContext.Set<BlueprintServiceAccountSession>()
            .Where(s => s.ServiceAccountId == serviceAccountId && s.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

        // Prime the fast-path marker so subsequent requests validate the session existence without a DB read.
        // The absolute expiry matches the token/session lifetime so the marker self-cleans.
        await this.SetCacheMarkerAsync(sessionKey, serviceAccountId, expiresAt, cancellationToken);
    }

    private async Task<Guid?> ResolveServiceAccountIdAsync(TContext dbContext, string sessionKey, CancellationToken cancellationToken)
    {
        var cached = await this.cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached) && Guid.TryParse(cached, CultureInfo.InvariantCulture, out var cachedId))
        {
            return cachedId;
        }

        // Fallback for a lost cache marker: honor the tracking row only while it is present and unexpired.
        // A revoked session has no row, so this correctly returns null. No re-prime (see ValidateSessionAsync).
        var now = DateTimeOffset.UtcNow;
        var rowAccountId = await dbContext.Set<BlueprintServiceAccountSession>()
            .AsNoTracking()
            .Where(s => s.SessionKey == sessionKey && s.ExpiresAt > now)
            .Select(s => (Guid?)s.ServiceAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        return rowAccountId;
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
