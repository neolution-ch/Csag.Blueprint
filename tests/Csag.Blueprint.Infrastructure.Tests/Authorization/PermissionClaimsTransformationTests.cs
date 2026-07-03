namespace Csag.Blueprint.Infrastructure.Tests.Authorization;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authorization;
using NSubstitute;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for <see cref="PermissionClaimsTransformation"/>, the <c>IClaimsTransformation</c>
/// that expands a user's roles into permission claims on every request. Pure in-memory logic —
/// the only collaborator is a mocked <see cref="IRolePermissionResolver"/>.
/// </summary>
public sealed class PermissionClaimsTransformationTests
{
    private readonly IRolePermissionResolver resolver = Substitute.For<IRolePermissionResolver>();

    private PermissionClaimsTransformation Sut => new(this.resolver);

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_ReturnsUnchangedAndNeverCallsResolver()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // no auth type => not authenticated

        var result = await this.Sut.TransformAsync(principal);

        result.ShouldBeSameAs(principal);
        result.FindAll(IdentityClaimTypes.Permission).ShouldBeEmpty();
        this.resolver.DidNotReceive().GetPermissionsForRole(Arg.Any<string>());
    }

    [Fact]
    public async Task TransformAsync_AuthenticatedWithRole_AddsPermissionClaimsForThatRole()
    {
        this.resolver.GetPermissionsForRole("Admin").Returns(new[] { "users.read", "users.write" });
        var principal = AuthenticatedPrincipal(("Admin", ClaimTypes.Role));

        var result = await this.Sut.TransformAsync(principal);

        result.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value)
            .ShouldBe(new[] { "users.read", "users.write" }, ignoreOrder: true);
    }

    [Fact]
    public async Task TransformAsync_OverlappingRoles_DeduplicatesPermissions()
    {
        this.resolver.GetPermissionsForRole("Admin").Returns(new[] { "users.read", "users.write" });
        this.resolver.GetPermissionsForRole("Auditor").Returns(new[] { "users.read", "audit.view" });
        var principal = AuthenticatedPrincipal(("Admin", ClaimTypes.Role), ("Auditor", ClaimTypes.Role));

        var result = await this.Sut.TransformAsync(principal);

        result.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value)
            .ShouldBe(new[] { "users.read", "users.write", "audit.view" }, ignoreOrder: true);
    }

    [Fact]
    public async Task TransformAsync_PermissionAlreadyPresent_DoesNotDuplicateIt()
    {
        this.resolver.GetPermissionsForRole("Admin").Returns(new[] { "users.read" });
        var principal = AuthenticatedPrincipal(
            ("Admin", ClaimTypes.Role),
            ("users.read", IdentityClaimTypes.Permission));

        var result = await this.Sut.TransformAsync(principal);

        result.FindAll(IdentityClaimTypes.Permission).Count(c => c.Value == "users.read").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_RoleWithNoPermissions_AddsNothing()
    {
        this.resolver.GetPermissionsForRole("Guest").Returns([]);
        var principal = AuthenticatedPrincipal(("Guest", ClaimTypes.Role));

        var result = await this.Sut.TransformAsync(principal);

        result.FindAll(IdentityClaimTypes.Permission).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_MutatesTheSamePrincipalInstance()
    {
        this.resolver.GetPermissionsForRole("Admin").Returns(new[] { "users.read" });
        var principal = AuthenticatedPrincipal(("Admin", ClaimTypes.Role));

        var result = await this.Sut.TransformAsync(principal);

        result.ShouldBeSameAs(principal);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(params (string Value, string Type)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
