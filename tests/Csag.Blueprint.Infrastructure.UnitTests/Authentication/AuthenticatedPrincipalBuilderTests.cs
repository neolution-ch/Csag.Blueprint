namespace Csag.Blueprint.Infrastructure.UnitTests.Authentication;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Moq;

/// <summary>
/// Unit tests for <see cref="AuthenticatedPrincipalBuilder{TUser}"/>. The builder composes profile,
/// tenant, and authorization claims and then applies the claims transformation. The load-bearing
/// contract pinned here is the platform-vs-tenant role separation: the user's global Identity roles
/// are handed to the <see cref="ITenantAuthorizationResolver"/> — which owns the scope filtering —
/// and only the resolver's output ever reaches the principal, so a global operational role can
/// never leak into a tenant by bypassing the resolver.
/// </summary>
public sealed class AuthenticatedPrincipalBuilderTests
{
    private static readonly Guid UserId = new Guid("44444444-0000-0000-0000-000000000001");
    private static readonly Guid TenantId = new Guid("44444444-0000-0000-0000-0000000000b1");

    [Fact]
    public async Task BuildAsync_ComposesProfileTenantAndAuthorizationClaims()
    {
        // Arrange
        var user = CreateUser();
        var userManager = CreateUserManager(user, globalRoles: []);
        var resolver = CreateResolver(roles: [TestRoles.TenantViewer], permissions: [TestPermissions.VehiclesRead]);
        var builder = CreateBuilder(userManager.Object, resolver.Object, CreatePassThroughTransformation().Object);

        // Act
        var principal = await builder.BuildAsync(user, TenantId, TestContext.Current.CancellationToken);

        // Assert — profile claims from the user, the tenant claim, and the resolver's authorization output.
        principal.FindFirst(ClaimTypes.NameIdentifier).ShouldNotBeNull().Value.ShouldBe(UserId.ToString());
        principal.FindFirst(ClaimTypes.Email).ShouldNotBeNull().Value.ShouldBe("alice@example.com");
        principal.FindFirst(ClaimTypes.Name).ShouldNotBeNull().Value.ShouldBe("alice@example.com");
        principal.FindFirst(IdentityClaimTypes.PreferredLanguage).ShouldNotBeNull().Value.ShouldBe("de-CH");
        principal.FindFirst(IdentityClaimTypes.TenantId).ShouldNotBeNull().Value.ShouldBe(TenantId.ToString());
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe([TestRoles.TenantViewer]);
        principal.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ShouldBe([TestPermissions.VehiclesRead]);

        // The identity must carry the application scheme so the cookie handler treats it as authenticated.
        principal.Identity.ShouldNotBeNull().AuthenticationType.ShouldBe(IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public async Task BuildAsync_GlobalRolesFlowThroughResolver_OnlyResolverOutputReachesPrincipal()
    {
        // Arrange — the user globally holds an operational role (contrived/legacy) and a platform role.
        // The resolver honors only the platform role; the builder must not write the raw global set.
        var user = CreateUser();
        var userManager = CreateUserManager(user, globalRoles: [TestRoles.TenantManager, TestRoles.PlatformAdmin]);
        var resolver = new Mock<ITenantAuthorizationResolver>();
        IEnumerable<string>? observedGlobalRoles = null;
        resolver
            .Setup(r => r.ResolveAsync(UserId, It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .Callback((Guid? _, IEnumerable<string> globalRoles, Guid? _, CancellationToken _) => observedGlobalRoles = globalRoles.ToList())
            .ReturnsAsync(([TestRoles.PlatformAdmin], [TestPermissions.TenantsManage]));
        var builder = CreateBuilder(userManager.Object, resolver.Object, CreatePassThroughTransformation().Object);

        // Act
        var principal = await builder.BuildAsync(user, TenantId, TestContext.Current.CancellationToken);

        // Assert — the resolver received the full global role set to apply its scope rule...
        observedGlobalRoles.ShouldNotBeNull().ShouldBe([TestRoles.TenantManager, TestRoles.PlatformAdmin], ignoreOrder: true);

        // ...and the principal carries exactly the resolver's output: the global operational role is gone.
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe([TestRoles.PlatformAdmin]);
        principal.FindAll(IdentityClaimTypes.Permission).Select(c => c.Value).ShouldBe([TestPermissions.TenantsManage]);
    }

    [Fact]
    public async Task BuildAsync_WithoutTenant_OmitsTenantClaimAndResolvesWithNullTenant()
    {
        // Arrange — tenant-less sign-in (e.g. invite-only onboarding before the first tenant exists).
        var user = CreateUser();
        var userManager = CreateUserManager(user, globalRoles: [TestRoles.PlatformAdmin]);
        var resolver = CreateResolver(roles: [TestRoles.PlatformAdmin], permissions: [TestPermissions.TenantsManage]);
        var builder = CreateBuilder(userManager.Object, resolver.Object, CreatePassThroughTransformation().Object);

        // Act
        var principal = await builder.BuildAsync(user, tenantId: null, TestContext.Current.CancellationToken);

        // Assert
        principal.FindFirst(IdentityClaimTypes.TenantId).ShouldBeNull();
        resolver.Verify(
            r => r.ResolveAsync(UserId, It.IsAny<IEnumerable<string>>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuildAsync_AppliesClaimsTransformationLast()
    {
        // Arrange — the transformation must observe the fully composed principal (so it can expand
        // roles into permissions) and its return value must be what the builder hands back.
        var user = CreateUser();
        var userManager = CreateUserManager(user, globalRoles: []);
        var resolver = CreateResolver(roles: [TestRoles.TenantViewer], permissions: []);

        var transformed = new ClaimsPrincipal(new ClaimsIdentity("TransformedScheme"));
        ClaimsPrincipal? observedInput = null;
        var transformation = new Mock<IClaimsTransformation>();
        transformation
            .Setup(t => t.TransformAsync(It.IsAny<ClaimsPrincipal>()))
            .Callback((ClaimsPrincipal p) => observedInput = p)
            .ReturnsAsync(transformed);
        var builder = CreateBuilder(userManager.Object, resolver.Object, transformation.Object);

        // Act
        var principal = await builder.BuildAsync(user, TenantId, TestContext.Current.CancellationToken);

        // Assert — the transformation ran on the composed principal and its output is returned as-is.
        observedInput.ShouldNotBeNull();
        observedInput.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe([TestRoles.TenantViewer]);
        observedInput.FindFirst(IdentityClaimTypes.TenantId).ShouldNotBeNull();
        principal.ShouldBeSameAs(transformed);
    }

    private static TestUser CreateUser()
    {
        return new TestUser
        {
            Id = UserId,
            Email = "alice@example.com",
            PreferredLanguage = "de-CH",
        };
    }

    private static Mock<UserManager<TestUser>> CreateUserManager(TestUser user, IList<string> globalRoles)
    {
        var store = new Mock<IUserStore<TestUser>>();
        var userManager = new Mock<UserManager<TestUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(globalRoles);
        return userManager;
    }

    private static Mock<ITenantAuthorizationResolver> CreateResolver(IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        var resolver = new Mock<ITenantAuthorizationResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((roles, permissions));
        return resolver;
    }

    private static Mock<IClaimsTransformation> CreatePassThroughTransformation()
    {
        var transformation = new Mock<IClaimsTransformation>();
        transformation
            .Setup(t => t.TransformAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ClaimsPrincipal p) => p);
        return transformation;
    }

    private static AuthenticatedPrincipalBuilder<TestUser> CreateBuilder(
        UserManager<TestUser> userManager,
        ITenantAuthorizationResolver resolver,
        IClaimsTransformation transformation)
    {
        return new AuthenticatedPrincipalBuilder<TestUser>(userManager, resolver, transformation);
    }
}
