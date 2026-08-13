namespace Csag.Blueprint.Infrastructure.Session;

using System.Linq.Expressions;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Generic implementation of <see cref="ISessionManager"/> that manages authentication sessions.
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TContext">The application database context type.</typeparam>
public sealed class SessionManager<TUser, TContext> : ISessionManager
    where TUser : BlueprintUser
    where TContext : DbContext
{
    private readonly ITicketCacheService ticketCacheService;
    private readonly UserManager<TUser> userManager;
    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly ITenantAuthorizationResolver tenantAuthorizationResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager{TUser, TContext}"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="userManager">The user manager used to reload profile and global (platform) role data when refreshing sessions.</param>
    /// <param name="dbContextFactory">The database context factory used to persist and query active session records.</param>
    /// <param name="tenantAuthorizationResolver">The shared resolver that composes the effective roles and permissions per session tenant.</param>
    public SessionManager(
        ITicketCacheService ticketCacheService,
        UserManager<TUser> userManager,
        IDbContextFactory<TContext> dbContextFactory,
        ITenantAuthorizationResolver tenantAuthorizationResolver)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.tenantAuthorizationResolver = tenantAuthorizationResolver ?? throw new ArgumentNullException(nameof(tenantAuthorizationResolver));
    }

    /// <inheritdoc/>
    public async Task TrackSessionAsync(Guid userId, string sessionKey, DateTimeOffset expiresAt, string? userAgent, string? ipAddress, Guid? currentTenantId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Set<BlueprintActiveSession>().Add(new BlueprintActiveSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionKey = sessionKey,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            CurrentTenantId = currentTenantId,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return this.RevokeSessionsCoreAsync(s => s.UserId == userId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> RevokeUserSessionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Only sessions currently scoped to the given tenant are revoked; the shared account's sessions in
        // other tenants keep their cached tickets and tracking rows.
        return this.RevokeSessionsCoreAsync(s => s.UserId == userId && s.CurrentTenantId == tenantId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> RevokeOtherUserSessionsAsync(Guid userId, string keepSessionKey, CancellationToken cancellationToken = default)
    {
        // A null/empty keep-key would degrade the filter to "revoke everything" and silently sign the caller
        // out too. Require it explicitly so that intent is a deliberate call to RevokeUserSessionsAsync, not an
        // accident. Sharing RevokeSessionsCoreAsync means the preserved session is excluded by the same filter
        // that snapshots the rows to delete, so it is never touched and no concurrently-tracked session can
        // strand a ticket.
        ArgumentException.ThrowIfNullOrEmpty(keepSessionKey);

        return this.RevokeSessionsCoreAsync(s => s.UserId == userId && s.SessionKey != keepSessionKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> RevokeTenantSessionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Every session scoped to the tenant is revoked regardless of user — used when the tenant itself is
        // removed so no cached ticket keeps authorizing requests against a tenant that no longer exists.
        return this.RevokeSessionsCoreAsync(s => s.CurrentTenantId == tenantId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Manual revocation must remove both the cached authentication ticket and the tracking row.
        // This immediately invalidates the specific session and ensures it no longer appears in session listings.
        await this.ticketCacheService.RemoveTicketAsync(sessionKey, cancellationToken);

        var deletedCount = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UntrackSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Normal logout uses ITicketStore.RemoveAsync to clean up the cache entry.
        // This method therefore removes only the database tracking record so we do not perform duplicate cache work.
        var deletedCount = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount > 0;
    }

    /// <inheritdoc/>
    public async Task<List<ActiveSessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.UserId == userId && s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ActiveSessionInfo
            {
                Id = s.Id,
                SessionKey = s.SessionKey,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                UserAgent = s.UserAgent,
                IpAddress = s.IpAddress,
                CurrentTenantId = s.CurrentTenantId,
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> RefreshUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await this.GetUserSessionsAsync(userId, cancellationToken);
        return await this.RefreshSessionsCoreAsync(userId, sessions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> RefreshUserSessionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Only sessions currently scoped to the given tenant are refreshed; the shared account's sessions in
        // other tenants are unaffected by a tenant-scoped authorization change.
        var sessions = (await this.GetUserSessionsAsync(userId, cancellationToken))
            .Where(s => s.CurrentTenantId == tenantId)
            .ToList();

        return await this.RefreshSessionsCoreAsync(userId, sessions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // This cleanup affects only the tracking table. The distributed cache governs actual ticket expiration separately.
        return await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> RevokeSessionsCoreAsync(
        Expression<Func<BlueprintActiveSession, bool>> filter,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Snapshot the matching rows' keys AND ids first, so the tickets removed and the rows deleted are
        // exactly the same set. Re-evaluating the filter for the delete would race with sessions tracked
        // between the two statements: such a session would lose its tracking row while keeping its cached
        // ticket — a usable session that no longer shows up in listings and cannot be revoked individually.
        var sessions = await dbContext.Set<BlueprintActiveSession>()
            .Where(filter)
            .Select(s => new { s.Id, s.SessionKey })
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return 0;
        }

        // Delete the tracking rows BEFORE removing the cached tickets. A concurrent sliding renewal
        // re-writes its ticket unconditionally and then extends its row; with rows already gone, that
        // extension reports "no row" and the renewal removes its own ticket (see
        // DistributedCacheTicketStore.RenewAsync), so every interleaving converges on a dead session.
        // With the opposite order there is a window (ticket removed, row still present) in which a
        // renewal both resurrects the ticket AND extends the row successfully, surviving revocation.
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var revoked = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var session in sessions)
        {
            await this.ticketCacheService.RemoveTicketAsync(session.SessionKey, cancellationToken);
        }

        return revoked;
    }

    private async Task<int> RefreshSessionsCoreAsync(
        Guid userId,
        List<ActiveSessionInfo> sessions,
        CancellationToken cancellationToken)
    {
        if (sessions.Count == 0)
        {
            return 0;
        }

        // Reload the latest user profile data first. If the user no longer exists, there is nothing to refresh.
        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return 0;
        }

        var globalRoles = await this.userManager.GetRolesAsync(user);

        var refreshed = 0;

        // Each session carries its own tenant, so authorization must be recomputed per tenant — but only
        // ONCE per distinct tenant, reusing the result for every session scoped to it. The shared resolver
        // owns the composition rule (platform-scope filtering of global roles, tenant-scoped roles,
        // role-derived permissions, direct grants), so refresh can never drift from sign-in or tenant switch.
        foreach (var tenantSessions in sessions.GroupBy(s => s.CurrentTenantId))
        {
            var (roles, permissions) = await this.tenantAuthorizationResolver.ResolveAsync(userId, globalRoles, tenantSessions.Key, cancellationToken);
            var roleList = roles.ToList();
            var permissionList = permissions.ToList();

            foreach (var session in tenantSessions)
            {
                var ticket = await this.ticketCacheService.GetTicketAsync(session.SessionKey, cancellationToken);
                if (ticket?.Principal.Identity is not ClaimsIdentity identity)
                {
                    continue;
                }

                // Apply tenant + profile + authorization claims via the shared composer, so refresh and
                // tenant switch write session claims identically and cannot drift.
                identity.ApplySessionClaims(user, session.CurrentTenantId, roleList, permissionList);

                // Cached tickets are always stored with an expiry. Skip anything unexpected rather than aborting
                // the refresh of this user's other valid sessions.
                if (ticket.Properties.ExpiresUtc is not { } expiresUtc)
                {
                    continue;
                }

                await this.ticketCacheService.SetTicketAsync(session.SessionKey, ticket, expiresUtc, cancellationToken);
                refreshed++;
            }
        }

        // Return only the sessions whose cached ticket was actually re-written, not those skipped above.
        return refreshed;
    }
}
