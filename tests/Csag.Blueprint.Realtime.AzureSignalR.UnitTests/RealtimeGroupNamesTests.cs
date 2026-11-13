namespace Csag.Blueprint.Realtime.AzureSignalR.UnitTests;

/// <summary>
/// Tests for the addressing scheme that keeps real-time delivery inside a tenant.
/// </summary>
public sealed class RealtimeGroupNamesTests
{
    [Fact]
    public void Tenant_DiffersPerTenant()
    {
        var first = RealtimeGroupNames.Tenant(Guid.NewGuid());
        var second = RealtimeGroupNames.Tenant(Guid.NewGuid());

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Tenant_IsStableForTheSameTenant()
    {
        var tenantId = Guid.NewGuid();

        RealtimeGroupNames.Tenant(tenantId).ShouldBe(RealtimeGroupNames.Tenant(tenantId));
    }

    [Fact]
    public void ServiceUserId_DiffersPerTenant_ForTheSameUser()
    {
        // The same person can belong to several tenants with different roles. If the identifier were
        // just the user id, a message meant for one tenant would reach a session that has switched
        // to another.
        var userId = Guid.NewGuid();

        var inTenantA = RealtimeGroupNames.ServiceUserId(Guid.NewGuid(), userId);
        var inTenantB = RealtimeGroupNames.ServiceUserId(Guid.NewGuid(), userId);

        inTenantA.ShouldNotBe(inTenantB);
    }

    [Fact]
    public void GroupNames_StayWithinTheServiceLengthLimit()
    {
        // Azure SignalR rejects group names longer than 1024 characters.
        RealtimeGroupNames.Tenant(Guid.NewGuid()).Length.ShouldBeLessThanOrEqualTo(1024);
        RealtimeGroupNames.ServiceUserId(Guid.NewGuid(), Guid.NewGuid()).Length.ShouldBeLessThanOrEqualTo(1024);
    }
}
