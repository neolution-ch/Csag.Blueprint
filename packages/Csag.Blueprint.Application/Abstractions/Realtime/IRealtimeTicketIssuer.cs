namespace Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// Issues the connection ticket a browser needs to join the real-time channel.
/// </summary>
public interface IRealtimeTicketIssuer
{
    /// <summary>
    /// Grants the caller the group membership implied by their tenant and mints a short-lived
    /// connection ticket.
    /// </summary>
    /// <remarks>
    /// Both the tenant and the user are resolved server-side from the authenticated principal, so
    /// there is nothing a caller can supply to widen what they receive.
    /// </remarks>
    /// <param name="tenantId">The tenant the session is scoped to.</param>
    /// <param name="userId">The application user identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>The ticket, or <see langword="null"/> when real-time is not configured.</returns>
    Task<RealtimeTicket?> IssueAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
