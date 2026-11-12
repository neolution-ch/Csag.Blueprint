namespace Csag.Blueprint.Application.UnitTests.Realtime;

using System.Reflection;
using Csag.Blueprint.Application.Abstractions.Realtime;

/// <summary>
/// Tests for the real-time envelope and the shape of the notifier contract.
/// </summary>
public sealed class RealtimeEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 21, 7, 12, TimeSpan.Zero);

    [Fact]
    public void For_StampsTheEventFromTheGivenClock()
    {
        var entityId = Guid.NewGuid();

        var realtimeEvent = RealtimeEvent.For("example.changed", entityId, new FixedTimeProvider(Now));

        realtimeEvent.EventName.ShouldBe("example.changed");
        realtimeEvent.EntityId.ShouldBe(entityId);
        realtimeEvent.OccurredAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RefusesABlankEventName(string eventName)
    {
        Should.Throw<ArgumentException>(() => new RealtimeEvent(eventName, Guid.NewGuid(), Now));
    }

    [Fact]
    public void Notifier_HasNoBroadcastToEveryoneMember()
    {
        // Every audience the interface can express is scoped to a tenant. A "notify all" overload would
        // make a cross-tenant leak a one-line mistake, so the interface cannot say it.
        var methods = typeof(IRealtimeNotifier).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        methods.ShouldNotBeEmpty();
        methods.ShouldAllBe(method => method.GetParameters().Any(parameter => parameter.Name == "tenantId"));
    }

    [Fact]
    public async Task NoOpService_IssuesNoTicket()
    {
        var service = new NoOpRealtimeService();

        var ticket = await service.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        ticket.ShouldBeNull();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
