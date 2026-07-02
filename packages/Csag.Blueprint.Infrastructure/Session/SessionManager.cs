namespace Csag.Blueprint.Infrastructure.Session;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager{TUser, TContext}"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="userManager">The user manager used to reload profile, role, and permission data when refreshing sessions.</param>
    /// <param name="dbContextFactory">The database context factory used to persist and query active session records.</param>
    public SessionManager(
        ITicketCacheService ticketCacheService,
        UserManager<TUser> userManager,
        IDbContextFactory<TContext> dbContextFactory)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
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
    public async Task<int> RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sessionKeys = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.UserId == userId)
            .Select(s => s.SessionKey)
            .ToListAsync(cancellationToken);

        foreach (var sessionKey in sessionKeys)
        {
            await this.ticketCacheService.RemoveTicketAsync(sessionKey, cancellationToken);
        }

        return await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
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
                SessionKey = s.SessionKey,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                UserAgent = s.UserAgent,
                IpAddress = s.IpAddress,
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> RefreshUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await this.GetUserSessionsAsync(userId, cancellationToken);

        // Reload the latest user profile data first. If the user no longer exists, there is nothing to refresh.
        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return 0;
        }

        // Reload fresh roles and permissions from the identity store so cached tickets reflect current authorization.
        var (roles, permissions) = await this.userManager.GetUserRolesAndPermissionsAsync(user, cancellationToken);

        foreach (var session in sessions)
        {
            var ticket = await this.ticketCacheService.GetTicketAsync(session.SessionKey, cancellationToken);
            if (ticket?.Principal.Identity is not ClaimsIdentity identity)
            {
                continue;
            }

            // Refresh claims using the same helpers used during sign-in so all sessions remain consistent.
            identity.SetUserProfileClaims(user);
            identity.SetAuthorizationClaims(roles, permissions);

            // Preserve the original ticket lifetime where possible. If no expiry is present, fall back to the default session window.
            var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(6);
            await this.ticketCacheService.SetTicketAsync(session.SessionKey, ticket, expiresUtc, cancellationToken);
        }

        return sessions.Count;
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
}
