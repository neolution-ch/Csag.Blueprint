namespace Csag.Blueprint.IntegrationTests.Session;

using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Forwards every call to the real <see cref="ITicketCacheService"/> and, on each ticket removal, records
/// what a probe saw at that moment. Revocation orders its two removals deliberately, and the end state is
/// identical either way, so the ordering can only be observed from inside one of the two calls.
/// </summary>
/// <param name="inner">The ticket cache to forward to.</param>
/// <param name="probe">Runs before the removal is forwarded; its result is appended to <see cref="RowPresentAtRemoval"/>.</param>
internal sealed class RowProbingTicketCacheService(ITicketCacheService inner, Func<string, Task<bool>> probe)
    : ITicketCacheService
{
    /// <summary>
    /// Gets what the probe returned at each <see cref="RemoveTicketAsync"/> call, in call order.
    /// </summary>
    public List<bool> RowPresentAtRemoval { get; } = [];

    /// <inheritdoc/>
    public Task<AuthenticationTicket?> GetTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
        => inner.GetTicketAsync(sessionKey, cancellationToken);

    /// <inheritdoc/>
    public Task SetTicketAsync(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
        => inner.SetTicketAsync(sessionKey, ticket, expiresUtc, cancellationToken);

    /// <inheritdoc/>
    public async Task RemoveTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        this.RowPresentAtRemoval.Add(await probe(sessionKey));
        await inner.RemoveTicketAsync(sessionKey, cancellationToken);
    }
}
