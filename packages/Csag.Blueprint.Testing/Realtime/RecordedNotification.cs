namespace Csag.Blueprint.Testing.Realtime
{
    using Csag.Blueprint.Application.Abstractions.Realtime;

    /// <summary>
    /// One notification as an endpoint asked for it.
    /// </summary>
    /// <param name="TenantId">The tenant the notification is scoped to.</param>
    /// <param name="UserIds">The users it addresses, or none for the whole tenant.</param>
    /// <param name="Event">The event it carries.</param>
    public sealed record RecordedNotification(Guid TenantId, IReadOnlyCollection<Guid> UserIds, RealtimeEvent Event);
}
