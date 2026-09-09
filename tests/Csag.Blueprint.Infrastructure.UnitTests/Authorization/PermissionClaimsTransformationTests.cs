namespace Csag.Blueprint.Infrastructure.UnitTests.Authorization;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authorization;
using Csag.Blueprint.Tests.Shared.Authorization;

/// <summary>
/// Unit tests for <see cref="PermissionClaimsTransformation"/> covering role-to-permission expansion,
/// the dedup guard for overlapping roles and pre-existing permission claims, and the unauthenticated
/// short-circuit. Claims transformations can run multiple times per request, so idempotency
/// (no duplicate permission claims on a second pass) is load-bearing.
/// </summary>
public sealed class PermissionClaimsTransformationTests
{
    [Fact]
    public async Task TransformAsync_RoleClaim_ExpandsToPermissionClaims()
    {
        // Arrange
        var transformation = CreateTransformation();
        var principal = CreateAuthenticatedPrincipal(TestRoles.TenantManager);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert — every permission conferred by the role is present, and the role claim survives.
        GetPermissions(result).ShouldBe(
            [TestPermissions.VehiclesRead, TestPermissions.VehiclesManage, TestPermissions.MembersManage],
            ignoreOrder: true);
        result.FindAll(ClaimTypes.Role).ShouldHaveSingleItem().Value.ShouldBe(TestRoles.TenantManager);
    }

    [Fact]
    public async Task TransformAsync_PlatformRole_ExpandsToPlatformPermissionsOnly()
    {
        // Arrange — the platform-scope role confers exactly the platform permissions; tenant
        // operational permissions must not appear as a side effect of the expansion.
        var transformation = CreateTransformation();
        var principal = CreateAuthenticatedPrincipal(TestRoles.PlatformAdmin);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        GetPermissions(result).ShouldBe([TestPermissions.TenantsManage]);
    }

    [Fact]
    public async Task TransformAsync_OverlappingRoles_DoNotDuplicatePermissionClaims()
    {
        // Arrange — TenantViewer and TenantManager both confer vehicles:read.
        var transformation = CreateTransformation();
        var principal = CreateAuthenticatedPrincipal(TestRoles.TenantViewer, TestRoles.TenantManager);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert — the union of both roles' permissions, each exactly once.
        GetPermissions(result).ShouldBe(
            [TestPermissions.VehiclesRead, TestPermissions.VehiclesManage, TestPermissions.MembersManage],
            ignoreOrder: true);
    }

    [Fact]
    public async Task TransformAsync_ExistingPermissionClaim_IsNotDuplicated()
    {
        // Arrange — the identity already carries a permission claim (e.g. baked in at sign-in, or a
        // previous transformation pass in the same request pipeline).
        var transformation = CreateTransformation();
        var identity = CreateAuthenticatedIdentity(TestRoles.TenantViewer);
        identity.AddClaim(new Claim(IdentityClaimTypes.Permission, TestPermissions.VehiclesRead));
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        GetPermissions(result).ShouldBe([TestPermissions.VehiclesRead]);
    }

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_ShortCircuits()
    {
        // Arrange — an identity without an authentication type is not authenticated; even a role
        // claim on it must not be expanded.
        var transformation = CreateTransformation();
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Role, TestRoles.TenantManager));
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
        GetPermissions(result).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_AuthenticatedPrincipalWithoutRoles_AddsNoPermissionClaims()
    {
        // Arrange
        var transformation = CreateTransformation();
        var principal = CreateAuthenticatedPrincipal();

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        GetPermissions(result).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_UnknownRole_AddsNoPermissionClaims()
    {
        // Arrange — a role the resolver does not know (e.g. removed from code but still in a ticket).
        var transformation = CreateTransformation();
        var principal = CreateAuthenticatedPrincipal("RetiredRole");

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        GetPermissions(result).ShouldBeEmpty();
    }

    private static PermissionClaimsTransformation CreateTransformation()
    {
        return new PermissionClaimsTransformation(new TestRolePermissionResolver());
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params string[] roles)
    {
        return new ClaimsPrincipal(CreateAuthenticatedIdentity(roles));
    }

    private static ClaimsIdentity CreateAuthenticatedIdentity(params string[] roles)
    {
        // An authentication type is what makes ClaimsIdentity.IsAuthenticated true.
        var identity = new ClaimsIdentity("TestScheme");
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return identity;
    }

    private static List<string> GetPermissions(ClaimsPrincipal principal)
    {
        return principal.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ToList();
    }
}
