namespace Csag.Blueprint.Testing.Realtime
{
    using System.Collections.Concurrent;
    using Csag.Blueprint.Application.Abstractions.Realtime;

    /// <summary>
    /// An <see cref="IRealtimeNotifier"/> that keeps every notification in memory.
    /// </summary>
    public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
    {
        private readonly ConcurrentQueue<RecordedNotification> notifications = new();

        /// <summary>
        /// Gets the notifications recorded since the last <see cref="Clear"/>, oldest first.
        /// </summary>
        public IReadOnlyList<RecordedNotification> Notifications => [.. this.notifications];

        /// <summary>
        /// Forgets every recorded notification.
        /// </summary>
        public void Clear() => this.notifications.Clear();

        /// <inheritdoc />
        public Task NotifyTenantAsync(Guid tenantId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
        {
            this.notifications.Enqueue(new RecordedNotification(tenantId, [], realtimeEvent));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task NotifyUserAsync(Guid tenantId, Guid userId, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
        {
            this.notifications.Enqueue(new RecordedNotification(tenantId, [userId], realtimeEvent));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task NotifyUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
        {
            this.notifications.Enqueue(new RecordedNotification(tenantId, [.. userIds], realtimeEvent));
            return Task.CompletedTask;
        }
    }
}
