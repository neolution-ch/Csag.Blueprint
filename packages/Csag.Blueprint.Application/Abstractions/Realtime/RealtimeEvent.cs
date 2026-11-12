namespace Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// The single envelope carried over the real-time channel.
/// </summary>
/// <remarks>
/// This type deliberately carries an identifier and nothing else. A message, once sent to a group,
/// cannot be re-authorized per recipient, and a tenant group holds users with different permission
/// sets. Clients therefore react by invalidating a query and refetching through the ordinary REST
/// endpoints, where the authorization policies and the tenant query filter still apply. That keeps
/// the channel structurally incapable of leaking a row, makes a dropped message self-healing, and
/// stays well clear of the 1 MB payload limit on the Azure SignalR data-plane REST API.
/// </remarks>
public sealed class RealtimeEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeEvent"/> class.
    /// </summary>
    /// <param name="eventName">The event name subscribers match on.</param>
    /// <param name="entityId">The identifier of the entity the event concerns.</param>
    /// <param name="occurredAt">The instant the originating change was committed.</param>
    public RealtimeEvent(string eventName, Guid entityId, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        this.EventName = eventName;
        this.EntityId = entityId;
        this.OccurredAt = occurredAt;
    }

    /// <summary>
    /// Gets the event name subscribers match on, for example <c>example.reservation.changed</c>.
    /// </summary>
    public string EventName { get; }

    /// <summary>
    /// Gets the identifier of the entity the event concerns.
    /// </summary>
    public Guid EntityId { get; }

    /// <summary>
    /// Gets the instant the originating change was committed.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Creates an event stamped with the current UTC time.
    /// </summary>
    /// <param name="eventName">The event name subscribers match on.</param>
    /// <param name="entityId">The identifier of the entity the event concerns.</param>
    /// <param name="timeProvider">The clock the event is stamped from.</param>
    /// <returns>The event to publish.</returns>
    public static RealtimeEvent For(string eventName, Guid entityId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new RealtimeEvent(eventName, entityId, timeProvider.GetUtcNow());
    }
}
