namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

/// <summary>
/// Custom ITicketStore implementation that stores authentication tickets in strongly-typed distributed cache (IDistributedCache{CacheId}).
/// This enables stateless authentication with immediate session revocation capability.
/// Stores whatever authentication ticket is provided (roles/permissions should be added before storage).
/// Session tracking is handled by cookie authentication events (OnSignedIn/OnSigningOut).
/// </summary>
public sealed class DistributedCacheTicketStore : ITicketStore
{
    private readonly ITicketCacheService ticketCacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheTicketStore"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    public DistributedCacheTicketStore(ITicketCacheService ticketCacheService)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
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
        var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(6);

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
    /// Renews an authentication ticket by updating it in the cache.
    /// Used for sliding expiration.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <param name="ticket">The updated authentication ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(6);
        await this.ticketCacheService.SetTicketAsync(key, ticket, expiresUtc);
    }
}
