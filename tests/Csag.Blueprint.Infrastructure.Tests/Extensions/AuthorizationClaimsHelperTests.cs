namespace Csag.Blueprint.Infrastructure.Tests.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Extensions;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AuthorizationClaimsHelper.SetAuthorizationClaims"/>. The contract is
/// "replace, not accumulate": every call must first clear existing role/permission claims so that a
/// session refresh cannot leak stale authorization. Pure in-memory logic, no collaborators.
/// </summary>
public sealed class AuthorizationClaimsHelperTests
{
    [Fact]
    public void SetAuthorizationClaims_ReplacesExistingRoleAndPermissionClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "OldRole"),
            new Claim(IdentityClaimTypes.Permission, "old.permission"),
        ]);

        identity.SetAuthorizationClaims(["NewRole"], ["new.permission"]);

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["NewRole"]);
        identity.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ShouldBe(["new.permission"]);
    }

    [Fact]
    public void SetAuthorizationClaims_EmptyLists_ClearAllRoleAndPermissionClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Auditor"),
            new Claim(IdentityClaimTypes.Permission, "users.read"),
        ]);

        identity.SetAuthorizationClaims([], []);

        identity.FindAll(ClaimTypes.Role).ShouldBeEmpty();
        identity.FindAll(IdentityClaimTypes.Permission).ShouldBeEmpty();
    }

    [Fact]
    public void SetAuthorizationClaims_LeavesUnrelatedClaimsUntouched()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(IdentityClaimTypes.TenantId, "tenant-1"),
            new Claim(ClaimTypes.Role, "OldRole"),
        ]);

        identity.SetAuthorizationClaims(["NewRole"], ["new.permission"]);

        identity.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe("user-1");
        identity.FindFirst(ClaimTypes.Email)!.Value.ShouldBe("user@example.com");
        identity.FindFirst(IdentityClaimTypes.TenantId)!.Value.ShouldBe("tenant-1");
    }

    [Fact]
    public void SetAuthorizationClaims_DuplicatesInInput_AreAddedAsIs()
    {
        var identity = new ClaimsIdentity();

        identity.SetAuthorizationClaims(["Admin", "Admin"], ["users.read", "users.read"]);

        identity.FindAll(ClaimTypes.Role).Count().ShouldBe(2);
        identity.FindAll(IdentityClaimTypes.Permission).Count().ShouldBe(2);
    }

    [Fact]
    public void SetAuthorizationClaims_CalledTwice_ReflectsOnlyTheSecondCall()
    {
        var identity = new ClaimsIdentity();

        identity.SetAuthorizationClaims(["Admin"], ["users.write"]);
        identity.SetAuthorizationClaims(["Viewer"], ["users.read"]);

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["Viewer"]);
        identity.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ShouldBe(["users.read"]);
    }
}
