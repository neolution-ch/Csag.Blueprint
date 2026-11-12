namespace Csag.Blueprint.Testing.UnitTests.Realtime;

using Csag.Blueprint.Application.Abstractions.Realtime;
using Csag.Blueprint.Testing.Realtime;

/// <summary>
/// Tests for the in-memory notifier integration tests swap in.
/// </summary>
public sealed class RecordingRealtimeNotifierTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Records_EachAudience_InOrder()
    {
        var notifier = new RecordingRealtimeNotifier();
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await notifier.NotifyTenantAsync(TenantId, Event("tenant"), ct);
        await notifier.NotifyUserAsync(TenantId, userId, Event("user"), ct);
        await notifier.NotifyUsersAsync(TenantId, [userId, otherUserId], Event("users"), ct);

        notifier.Notifications.Select(notification => notification.Event.EventName).ShouldBe(["tenant", "user", "users"]);
        notifier.Notifications[0].UserIds.ShouldBeEmpty();
        notifier.Notifications[1].UserIds.ShouldBe([userId]);
        notifier.Notifications[2].UserIds.ShouldBe([userId, otherUserId]);
        notifier.Notifications.ShouldAllBe(notification => notification.TenantId == TenantId);
    }

    [Fact]
    public async Task Clear_ForgetsEverything()
    {
        var notifier = new RecordingRealtimeNotifier();
        await notifier.NotifyTenantAsync(TenantId, Event("tenant"), TestContext.Current.CancellationToken);

        notifier.Clear();

        notifier.Notifications.ShouldBeEmpty();
    }

    private static RealtimeEvent Event(string name) => new(name, Guid.NewGuid(), DateTimeOffset.UnixEpoch);
}
