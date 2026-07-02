namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Service for managing authentication ticket caching operations.
/// Handles serialization, deserialization, and cache I/O for authentication tickets.
/// </summary>
public interface ITicketCacheService
{
    /// <summary>
    /// Retrieves an authentication ticket from the cache.
    /// </summary>
    /// <param name="sessionKey">The session key that identifies the ticket.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authentication ticket, or null if not found or expired.</returns>
    Task<AuthenticationTicket?> GetTicketAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an authentication ticket in the cache.
    /// </summary>
    /// <param name="sessionKey">The session key that identifies the ticket.</param>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <param name="expiresUtc">The absolute expiration time in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetTicketAsync(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an authentication ticket from the cache.
    /// </summary>
    /// <param name="sessionKey">The session key that identifies the ticket.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveTicketAsync(string sessionKey, CancellationToken cancellationToken = default);
}
