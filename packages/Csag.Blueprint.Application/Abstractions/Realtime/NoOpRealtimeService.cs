namespace Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// The implementation used when real-time is switched off.
/// </summary>
/// <remarks>
/// Registering this rather than leaving the services unregistered is what lets calling code publish
/// unconditionally, with no feature check at the call site. A generated project that never enables
/// real-time therefore carries no branching and no cloud dependency.
/// </remarks>
public sealed class NoOpRealtimeService : IRealtimeNotifier, IRealtimeTicketIssuer
{
    /// <inheritdoc />
    public Task NotifyTenantAsync(Guid tenantId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NotifyUserAsync(Guid tenantId, Guid userId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NotifyUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RealtimeTicket?> IssueAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<RealtimeTicket?>(null);
    }
}
