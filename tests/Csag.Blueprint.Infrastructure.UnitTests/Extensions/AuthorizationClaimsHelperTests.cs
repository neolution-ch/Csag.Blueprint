namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Extensions;

/// <summary>
/// Unit tests for <see cref="AuthorizationClaimsHelper"/> covering the replace semantics of role
/// and permission claims and pinning the claim type constants written to the ticket.
/// </summary>
public sealed class AuthorizationClaimsHelperTests
{
    [Fact]
    public void SetAuthorizationClaims_AddsRoleAndPermissionClaims()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        identity.SetAuthorizationClaims(["TenantManager", "TenantViewer"], ["vehicles:read", "vehicles:manage"]);

        // Assert
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .ShouldBe(["TenantManager", "TenantViewer"], ignoreOrder: true);
        identity.FindAll("Permission").Select(c => c.Value)
            .ShouldBe(["vehicles:read", "vehicles:manage"], ignoreOrder: true);
    }

    [Fact]
    public void SetAuthorizationClaims_WritesPinnedClaimTypes()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        identity.SetAuthorizationClaims(["TenantViewer"], ["vehicles:read"]);

        // Assert — pin the wire-level claim types: authorization checks match on these strings,
        // so any drift silently breaks every issued ticket.
        identity.Claims.Select(c => c.Type)
            .ShouldBe([ClaimTypes.Role, "Permission"], ignoreOrder: true);
    }

    [Fact]
    public void SetAuthorizationClaims_ReplacesAllExistingRoleAndPermissionClaims()
    {
        // Arrange — a stale ticket with multiple roles and permissions.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Role, "OldRole1"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "OldRole2"));
        identity.AddClaim(new Claim("Permission", "old:permission"));

        // Act
        identity.SetAuthorizationClaims(["NewRole"], ["new:permission"]);

        // Assert — every existing role/permission claim is removed, not just the first match.
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["NewRole"]);
        identity.FindAll("Permission").Select(c => c.Value).ShouldBe(["new:permission"]);
    }

    [Fact]
    public void SetAuthorizationClaims_WithEmptyLists_RemovesAllRoleAndPermissionClaims()
    {
        // Arrange — e.g. a refresh after the user lost every role in the active tenant.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Role, "OldRole"));
        identity.AddClaim(new Claim("Permission", "old:permission"));

        // Act
        identity.SetAuthorizationClaims([], []);

        // Assert
        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
        identity.FindAll("Permission").ShouldBeEmpty();
    }

    [Fact]
    public void SetAuthorizationClaims_LeavesUnrelatedClaimsIntact()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Email, "alice@example.com"));
        identity.AddClaim(new Claim("TenantId", Guid.NewGuid().ToString()));

        // Act
        identity.SetAuthorizationClaims(["TenantViewer"], ["vehicles:read"]);

        // Assert — only role/permission claims are managed by this helper.
        identity.FindFirst(ClaimTypes.Email).ShouldNotBeNull();
        identity.FindFirst("TenantId").ShouldNotBeNull();
    }

    [Fact]
    public void SetAuthorizationClaims_PreservesDuplicateInputValues()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act — the helper adds each provided value verbatim without de-duplicating.
        identity.SetAuthorizationClaims(["TenantViewer", "TenantViewer"], ["vehicles:read", "vehicles:read"]);

        // Assert
        identity.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
        identity.FindAll("Permission").Count().ShouldBe(2);
    }
}
