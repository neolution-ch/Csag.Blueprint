namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Enums;
using Microsoft.AspNetCore.Authentication;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Implementation of ITicketCacheService that manages ticket caching in a distributed cache.
/// Handles serialization, deserialization, and cache I/O for authentication tickets.
/// </summary>
public sealed class TicketCacheService : ITicketCacheService
{
    private readonly IDistributedCache<CacheId> cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketCacheService"/> class.
    /// </summary>
    /// <param name="cache">The strongly-typed distributed cache instance.</param>
    public TicketCacheService(IDistributedCache<CacheId> cache)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc/>
    public async Task<AuthenticationTicket?> GetTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        var bytes = await this.cache.GetAsync<byte[]>(CacheId.AuthTicket, sessionKey, cancellationToken);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var ticket = TicketSerializer.Default.Deserialize(bytes);
        return ticket;
    }

    /// <inheritdoc/>
    public async Task SetTicketAsync(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
    {
        var bytes = TicketSerializer.Default.Serialize(ticket);

        var cacheOptions = new CacheEntryOptions
        {
            AbsoluteExpiration = expiresUtc,
        };

        await this.cache.SetWithOptionsAsync(CacheId.AuthTicket, sessionKey, bytes, cacheOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        await this.cache.RemoveAsync(CacheId.AuthTicket, sessionKey, cancellationToken);
    }
}
