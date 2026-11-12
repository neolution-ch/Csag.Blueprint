namespace Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// Publishes real-time notifications to connected clients.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no "notify everyone" member. Every generated project on this blueprint is
/// multi-tenant, and omitting the broadcast overload is the cheapest available defence against a
/// cross-tenant leak: the mistake cannot be made because the interface has no way to express it.
/// </para>
/// <para>
/// Implementations must never throw into a caller. A notification is a best-effort hint that some
/// data changed; the write that triggered it has already been committed, so turning a transport
/// failure into an error would make the client retry a request that succeeded.
/// </para>
/// </remarks>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Notifies every connection belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant whose members should be notified.</param>
    /// <param name="realtimeEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>A task that completes once the publish attempt has finished.</returns>
    Task NotifyTenantAsync(Guid tenantId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies every connection belonging to one user within one tenant.
    /// </summary>
    /// <param name="tenantId">The tenant the user is acting in.</param>
    /// <param name="userId">The application user identifier.</param>
    /// <param name="realtimeEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>A task that completes once the publish attempt has finished.</returns>
    Task NotifyUserAsync(Guid tenantId, Guid userId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies every connection belonging to each of several users within one tenant.
    /// </summary>
    /// <remarks>
    /// Each user receives the event once, however often they appear in <paramref name="userIds"/>, and
    /// an empty collection publishes nothing. Message quota is counted per receiving connection, so this
    /// costs the same as calling <see cref="NotifyUserAsync"/> for each user; what it saves is the loop
    /// and the sequential round trips.
    /// </remarks>
    /// <param name="tenantId">The tenant the users are acting in.</param>
    /// <param name="userIds">The application user identifiers.</param>
    /// <param name="realtimeEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>A task that completes once the publish attempt has finished.</returns>
    Task NotifyUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default);
}
