namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Extensions;

/// <summary>
/// Unit tests for <see cref="SessionClaimsHelper"/> verifying that a session ticket's identity gets
/// the tenant, profile, and authorization claims composed the same way for every rebuild path
/// (session refresh and tenant switch), including the tenant-less session case.
/// </summary>
public sealed class SessionClaimsHelperTests
{
    private static readonly Guid UserId = new Guid("66666666-0000-0000-0000-000000000001");
    private static readonly Guid TenantId = new Guid("66666666-0000-0000-0000-000000000002");

    [Fact]
    public void ApplySessionClaims_WithTenant_WritesTenantProfileAndAuthorizationClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        identity.ApplySessionClaims(CreateUser(), TenantId, ["TenantManager"], ["vehicles:manage"]);

        // Assert — all three claim groups are composed in one pass.
        identity.FindFirst("TenantId").ShouldNotBeNull().Value.ShouldBe(TenantId.ToString());
        identity.FindFirst(ClaimTypes.NameIdentifier).ShouldNotBeNull().Value.ShouldBe(UserId.ToString());
        identity.FindFirst(ClaimTypes.Email).ShouldNotBeNull().Value.ShouldBe("alice@example.com");
        identity.FindFirst(ClaimTypes.Name).ShouldNotBeNull().Value.ShouldBe("Alice Example");
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["TenantManager"]);
        identity.FindAll("Permission").Select(c => c.Value).ShouldBe(["vehicles:manage"]);
    }

    [Fact]
    public void ApplySessionClaims_WithoutTenant_WritesNoTenantClaim()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act — a tenant-less session (e.g. a user with no memberships yet).
        identity.ApplySessionClaims(CreateUser(), tenantId: null, [], []);

        // Assert
        identity.FindFirst("TenantId").ShouldBeNull();
        identity.FindFirst(ClaimTypes.Email).ShouldNotBeNull();
    }

    [Fact]
    public void ApplySessionClaims_OnTenantSwitch_ReplacesTenantAndAuthorizationClaims()
    {
        // Arrange — a ticket built for the old tenant.
        var identity = new ClaimsIdentity();
        identity.ApplySessionClaims(CreateUser(), TenantId, ["TenantManager"], ["vehicles:manage"]);
        var newTenantId = Guid.NewGuid();

        // Act — rebuild the same identity for the target tenant with its resolved authorization.
        identity.ApplySessionClaims(CreateUser(), newTenantId, ["TenantViewer"], ["vehicles:read"]);

        // Assert — exactly one tenant claim, and the old tenant's authorization is gone.
        identity.FindAll("TenantId").ShouldHaveSingleItem().Value.ShouldBe(newTenantId.ToString());
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["TenantViewer"]);
        identity.FindAll("Permission").Select(c => c.Value).ShouldBe(["vehicles:read"]);
    }

    [Fact]
    public void ApplySessionClaims_WithoutTenant_LeavesExistingTenantClaimInPlace()
    {
        // Arrange — a ticket that already carries a tenant claim.
        var identity = new ClaimsIdentity();
        identity.ApplySessionClaims(CreateUser(), TenantId, [], []);

        // Act — reapply for a tenant-less session.
        identity.ApplySessionClaims(CreateUser(), tenantId: null, [], []);

        // Assert — the tenant claim is only written when a tenant is active, never removed, so a
        // stale claim from the previous tenant survives a switch to a tenant-less session.
        identity.FindFirst("TenantId").ShouldNotBeNull().Value.ShouldBe(TenantId.ToString());
    }

    [Fact]
    public void ApplySessionClaims_WithNullIdentity_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => SessionClaimsHelper.ApplySessionClaims(null!, CreateUser(), TenantId, [], []));
    }

    private static TestUserProfileClaimsSource CreateUser() => new()
    {
        Id = UserId,
        Email = "alice@example.com",
        DisplayName = "Alice Example",
        PreferredLanguage = "de-CH",
    };
}
