namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom ITicketStore implementation that stores authentication tickets in strongly-typed distributed cache (IDistributedCache{CacheId}).
/// This enables stateless authentication with immediate session revocation capability.
/// Stores whatever authentication ticket is provided (roles/permissions should be added before storage).
/// Session tracking is handled by cookie authentication events (OnSignedIn/OnSigningOut).
/// </summary>
public sealed class DistributedCacheTicketStore : ITicketStore
{
    private readonly ITicketCacheService ticketCacheService;
    private readonly ISessionExpirationExtender sessionExpirationExtender;
    private readonly ILogger<DistributedCacheTicketStore> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheTicketStore"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="sessionExpirationExtender">The extender that keeps the tracked session row's expiration in step with renewed tickets.</param>
    /// <param name="logger">The logger.</param>
    public DistributedCacheTicketStore(
        ITicketCacheService ticketCacheService,
        ISessionExpirationExtender sessionExpirationExtender,
        ILogger<DistributedCacheTicketStore> logger)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.sessionExpirationExtender = sessionExpirationExtender ?? throw new ArgumentNullException(nameof(sessionExpirationExtender));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores an authentication ticket in the distributed cache and returns a session key.
    /// Expects the ticket to already contain roles and permissions as claims.
    /// Session tracking is handled by OnSignedIn cookie authentication event.
    /// </summary>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <returns>A unique session key that identifies this ticket in the cache.</returns>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        // The cookie handler always sets ExpiresUtc from ExpireTimeSpan before calling StoreAsync,
        // so a null here means the authentication pipeline is misconfigured rather than a state to paper over.
        // A common cause is a non-persistent sign-in (IsPersistent = false): the cookie handler only sets
        // ExpiresUtc when IsPersistent = true (i.e. AuthenticationProperties.IsPersistent must be set to true).
        var expiresUtc = ticket.Properties.ExpiresUtc
            ?? throw new InvalidOperationException(
                "Authentication ticket has no ExpiresUtc when storing the session. "
                + "Ensure the sign-in uses IsPersistent = true so the cookie handler sets ExpiresUtc from ExpireTimeSpan before StoreAsync runs.");

        // Store session key in ticket properties for access in cookie authentication events
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName] = sessionKey;

        // Store in cache
        await this.ticketCacheService.SetTicketAsync(sessionKey, ticket, expiresUtc);

        return sessionKey;
    }

    /// <summary>
    /// Retrieves an authentication ticket from the distributed cache.
    /// Returns the cached ticket with roles and permissions as they were at login.
    /// To refresh roles/permissions after changes, use ISessionManager.RefreshUserSessionsAsync().
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <returns>The authentication ticket, or null if not found or expired.</returns>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        return await this.ticketCacheService.GetTicketAsync(key);
    }

    /// <summary>
    /// Removes an authentication ticket from the distributed cache.
    /// Used for logout and session revocation.
    /// Session untracking is handled by OnSigningOut cookie authentication event.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAsync(string key)
    {
        await this.ticketCacheService.RemoveTicketAsync(key);
    }

    /// <summary>
    /// Renews an authentication ticket by updating it in the cache and extending the tracked
    /// session row's expiration to match. Used for sliding expiration.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <param name="ticket">The updated authentication ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Sliding renewal computes and assigns a fresh ExpiresUtc before calling RenewAsync,
        // so a null here means the authentication pipeline is misconfigured rather than a state to paper over.
        // A common cause is a non-persistent sign-in (IsPersistent = false): the cookie handler only sets
        // ExpiresUtc when IsPersistent = true (i.e. AuthenticationProperties.IsPersistent must be set to true).
        var expiresUtc = ticket.Properties.ExpiresUtc
            ?? throw new InvalidOperationException(
                "Authentication ticket has no ExpiresUtc when renewing the session. "
                + "Ensure the sign-in uses IsPersistent = true so the cookie handler sets ExpiresUtc during sliding renewal before RenewAsync runs.");

        await this.ticketCacheService.SetTicketAsync(key, ticket, expiresUtc);

        // Keep the tracked session row's ExpiresAt in step with the renewed ticket. Otherwise a
        // long-lived sliding session outlives its row and disappears from session listing,
        // revocation, and refresh — so an authorization change (e.g. a demotion) would never
        // reach a user who simply stays active.
        bool sessionStillTracked;
        try
        {
            sessionStillTracked = await this.sessionExpirationExtender.ExtendAsync(key, expiresUtc);
        }
        catch (Exception ex)
        {
            // A failed row update must not fail the renewal itself: the cache ticket is already
            // renewed, and the row merely lags (the pre-extension behavior) until the next
            // successful renewal. Only a definitive "no row" answer (below) is treated as revoked.
            this.logger.LogWarning(ex, "Failed to extend tracked session expiration for session {SessionKey}", key);
            return;
        }

        // No tracking row means the session was revoked (or its lagging row was cleaned up as expired)
        // while this renewal was in flight. The SetTicketAsync above has just resurrected the ticket of
        // a session that no longer exists administratively: it would be invisible to session listing,
        // unreachable by revocation and refresh, and would keep sliding forever. Fail closed by removing
        // the ticket again — at worst a legitimately active user has to sign in again.
        if (!sessionStillTracked)
        {
            await this.ticketCacheService.RemoveTicketAsync(key);
            this.logger.LogWarning("Session {SessionKey} was renewed concurrently with its revocation; the renewed ticket has been removed.", key);
        }
    }
}
