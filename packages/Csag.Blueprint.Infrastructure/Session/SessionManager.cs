namespace Csag.Blueprint.Infrastructure.Session;

using System.Linq.Expressions;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The tracked-session operations <see cref="SessionLifecycle{TUser, TContext}"/> composes its intents from: revoking,
/// listing, refreshing and re-scoping sessions, each of which keeps the cached tickets and the tracking rows in step.
/// </summary>
/// <remarks>
/// Every operation runs to completion rather than observing a cancellation token, for the reason the intents give.
/// </remarks>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TContext">The application database context type.</typeparam>
internal sealed class SessionManager<TUser, TContext>
    where TUser : BlueprintUser
    where TContext : DbContext
{
    private readonly ITicketCacheService ticketCacheService;
    private readonly UserManager<TUser> userManager;
    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly ITenantAuthorizationResolver tenantAuthorizationResolver;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager{TUser, TContext}"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="userManager">The user manager used to reload profile and global (platform) role data when refreshing sessions.</param>
    /// <param name="dbContextFactory">The database context factory used to query and change active session records.</param>
    /// <param name="tenantAuthorizationResolver">The shared resolver that composes the effective roles and permissions per session tenant.</param>
    /// <param name="timeProvider">The clock used to evaluate session expiry.</param>
    public SessionManager(
        ITicketCacheService ticketCacheService,
        UserManager<TUser> userManager,
        IDbContextFactory<TContext> dbContextFactory,
        ITenantAuthorizationResolver tenantAuthorizationResolver,
        TimeProvider timeProvider)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.tenantAuthorizationResolver = tenantAuthorizationResolver ?? throw new ArgumentNullException(nameof(tenantAuthorizationResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Ends every session matching <paramref name="filter"/>: removes the cached tickets, deletes the tracking rows
    /// of the sessions whose tickets are gone, then removes those tickets once more.
    /// </summary>
    /// <param name="filter">Selects the sessions to end.</param>
    /// <param name="failures">Collects the ticket removals that failed. A session whose ticket could not be removed
    /// keeps its tracking row, so running the revocation again finds it.</param>
    /// <returns>The keys of the sessions the filter matched.</returns>
    public async Task<IReadOnlyList<string>> RevokeAsync(Expression<Func<BlueprintActiveSession, bool>> filter, List<Exception> failures)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(CancellationToken.None);

        // Snapshot the matching rows' keys AND ids first, so the rows deleted are exactly those of the sessions
        // whose tickets were removed. Re-evaluating the filter for the delete would race with sessions tracked
        // between the statements: such a session would lose its tracking row while keeping its cached ticket —
        // a usable session that no longer shows up in listings and cannot be revoked individually.
        var sessions = await dbContext.Set<BlueprintActiveSession>()
            .Where(filter)
            .Select(s => new { s.Id, s.SessionKey })
            .ToListAsync(CancellationToken.None);

        if (sessions.Count == 0)
        {
            return [];
        }

        // Tickets first, while every row still names its session: the row is the only way to find a session again,
        // so a session whose ticket could not be removed keeps it, and a retry of the intent ends that session.
        var removed = await this.RemoveTicketsAsync(sessions.Select(s => s.SessionKey), failures);

        var removedIds = sessions.Where(s => removed.Contains(s.SessionKey)).Select(s => s.Id).ToList();
        if (removedIds.Count > 0)
        {
            await dbContext.Set<BlueprintActiveSession>()
                .Where(s => removedIds.Contains(s.Id))
                .ExecuteDeleteAsync(CancellationToken.None);

            // A concurrent sliding renewal that read its ticket before the removal above writes it back, and keeps
            // it when it renews its row before the delete. Removing the tickets again once the rows are gone ends
            // such a session; a renewal that comes later finds no row and removes its own ticket (see
            // DistributedCacheTicketStore.RenewAsync).
            await this.RemoveTicketsAsync(removed, failures);
        }

        return sessions.Select(s => s.SessionKey).ToList();
    }

    /// <summary>
    /// Gets the account's sessions that have not expired, newest first.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The account's sessions, keys included.</returns>
    public async Task<List<TrackedSession>> GetUserSessionsAsync(Guid userId)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var now = this.timeProvider.GetUtcNow();

        return await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.UserId == userId && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new TrackedSession(s.Id, s.SessionKey, s.CreatedAt, s.ExpiresAt, s.UserAgent, s.IpAddress, s.CurrentTenantId))
            .ToListAsync(CancellationToken.None);
    }

    /// <summary>
    /// Rebuilds the cached claims of the account's sessions, each in its own tenant.
    /// </summary>
    /// <param name="userId">The account whose sessions are refreshed.</param>
    /// <param name="tenantId">Limits the refresh to sessions scoped to this tenant; <see langword="null"/> refreshes every session.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshAsync(Guid userId, Guid? tenantId)
    {
        var sessions = await this.GetUserSessionsAsync(userId);
        if (tenantId.HasValue)
        {
            // Sessions the (shared) account holds in other tenants are unaffected by a tenant-scoped change.
            sessions = sessions.Where(s => s.CurrentTenantId == tenantId).ToList();
        }

        if (sessions.Count == 0)
        {
            return;
        }

        // Reload the latest user profile data first. If the user no longer exists, there is nothing to refresh.
        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return;
        }

        var globalRoles = await this.userManager.GetRolesAsync(user);
        var rewritten = new List<string>();

        // Each session carries its own tenant, so authorization must be recomputed per tenant — but only
        // ONCE per distinct tenant, reusing the result for every session scoped to it. The shared resolver
        // owns the composition rule (platform-scope filtering of global roles, tenant-scoped roles,
        // role-derived permissions, direct grants), so refresh can never drift from sign-in or tenant switch.
        foreach (var tenantSessions in sessions.GroupBy(s => s.CurrentTenantId))
        {
            var (roles, permissions) = await this.tenantAuthorizationResolver.ResolveAsync(userId, globalRoles, tenantSessions.Key, CancellationToken.None);
            var roleList = roles.ToList();
            var permissionList = permissions.ToList();

            foreach (var session in tenantSessions)
            {
                var ticket = await this.ticketCacheService.GetTicketAsync(session.SessionKey, CancellationToken.None);
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

                await this.ticketCacheService.SetTicketAsync(session.SessionKey, ticket, expiresUtc, CancellationToken.None);
                rewritten.Add(session.SessionKey);
            }
        }

        await this.RemoveUntrackedTicketsAsync(rewritten);
    }

    /// <summary>
    /// Moves a session to another tenant: rebuilds its claims for the tenant, rewrites its ticket, and records the
    /// tenant on its tracking row. The caller has already checked the account's membership.
    /// </summary>
    /// <param name="sessionKey">The session to move.</param>
    /// <param name="userId">The account the session must belong to.</param>
    /// <param name="tenantId">The tenant to move the session to.</param>
    /// <returns>What happened.</returns>
    public async Task<SessionTenantSwitchOutcome> SwitchTenantAsync(string sessionKey, Guid userId, Guid tenantId)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var now = this.timeProvider.GetUtcNow();

        // Only the session the request authenticated with moves — never another session of the same account.
        var tracked = await dbContext.Set<BlueprintActiveSession>()
            .AnyAsync(s => s.SessionKey == sessionKey && s.UserId == userId && s.ExpiresAt > now, CancellationToken.None);
        if (!tracked)
        {
            return SessionTenantSwitchOutcome.NoActiveSession;
        }

        var ticket = await this.ticketCacheService.GetTicketAsync(sessionKey, CancellationToken.None);
        if (ticket?.Principal.Identity is not ClaimsIdentity identity || ticket.Properties.ExpiresUtc is not { } expiresUtc)
        {
            return SessionTenantSwitchOutcome.SessionExpired;
        }

        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return SessionTenantSwitchOutcome.SessionExpired;
        }

        var originalTicket = TicketSerializer.Default.Serialize(ticket);
        var globalRoles = await this.userManager.GetRolesAsync(user);
        var (roles, permissions) = await this.tenantAuthorizationResolver.ResolveAsync(userId, globalRoles, tenantId, CancellationToken.None);
        identity.ApplySessionClaims(user, tenantId, roles.ToList(), permissions.ToList());

        // Ticket first, then the row — the order the ticket store renews in — so a revocation that runs in between
        // either finds the row and removes the new ticket, or has already deleted the row, which the update below
        // then reports.
        await this.ticketCacheService.SetTicketAsync(sessionKey, ticket, expiresUtc, CancellationToken.None);

        int moved;
        try
        {
            moved = await dbContext.Set<BlueprintActiveSession>()
                .Where(s => s.SessionKey == sessionKey && s.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.CurrentTenantId, tenantId), CancellationToken.None);
        }
        catch (Exception)
        {
            // Requests are authorized from the ticket, while tenant-scoped revocation finds sessions by the row's
            // tenant. A switched ticket under a row still naming the previous tenant would be out of reach of a
            // revocation in the new one, so the original ticket goes back before the failure surfaces.
            await this.ticketCacheService.SetTicketAsync(sessionKey, TicketSerializer.Default.Deserialize(originalTicket)!, expiresUtc, CancellationToken.None);
            throw;
        }

        if (moved == 0)
        {
            // The session was revoked while its ticket was being rewritten; the rewrite must not bring it back.
            await this.ticketCacheService.RemoveTicketAsync(sessionKey, CancellationToken.None);
            return SessionTenantSwitchOutcome.NoActiveSession;
        }

        return SessionTenantSwitchOutcome.Switched;
    }

    private async Task RemoveUntrackedTicketsAsync(List<string> sessionKeys)
    {
        if (sessionKeys.Count == 0)
        {
            return;
        }

        // A revocation that ran while the tickets were being rewritten deleted rows whose tickets were then written
        // back. Such a ticket would authenticate with no row to list or revoke it by, so it goes again.
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var stillTracked = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => sessionKeys.Contains(s.SessionKey))
            .Select(s => s.SessionKey)
            .ToListAsync(CancellationToken.None);

        var failures = new List<Exception>();
        await this.RemoveTicketsAsync(sessionKeys.Except(stillTracked, StringComparer.Ordinal), failures);
        if (failures.Count > 0)
        {
            throw new AggregateException("Removing the cached tickets of revoked sessions failed.", failures);
        }
    }

    private async Task<List<string>> RemoveTicketsAsync(IEnumerable<string> sessionKeys, List<Exception> failures)
    {
        // Every removal is attempted even when one fails: each ticket left behind keeps a session alive.
        var removed = new List<string>();
        foreach (var sessionKey in sessionKeys)
        {
            try
            {
                await this.ticketCacheService.RemoveTicketAsync(sessionKey, CancellationToken.None);
                removed.Add(sessionKey);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        return removed;
    }

    /// <summary>
    /// A tracked session as the session lifecycle reads it, key included.
    /// </summary>
    /// <param name="Id">The tracking row's identifier.</param>
    /// <param name="SessionKey">The ticket store key the session cookie carries.</param>
    /// <param name="CreatedAt">When the session was created.</param>
    /// <param name="ExpiresAt">When the session expires.</param>
    /// <param name="UserAgent">The user agent of the request that signed in.</param>
    /// <param name="IpAddress">The client address of the request that signed in.</param>
    /// <param name="CurrentTenantId">The tenant the session is scoped to.</param>
    internal sealed record TrackedSession(
        Guid Id,
        string SessionKey,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        string? UserAgent,
        string? IpAddress,
        Guid? CurrentTenantId);
}
