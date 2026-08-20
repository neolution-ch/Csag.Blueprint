namespace Csag.Blueprint.Infrastructure.UnitTests.Tenancy;

using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Tenancy;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Unit tests for <see cref="TenantAuthorizationResolver"/>, focused on the two-scope composition rule:
/// effective roles = the user's PLATFORM-SCOPE global roles unioned with their tenant-scoped roles for the
/// active tenant. The load-bearing guarantee is that an operational role sitting in the global role set is
/// NOT honored inside a tenant — otherwise a global operational-role assignment (e.g. from OAuth
/// auto-provisioning or legacy data) would leak into every tenant.
/// </summary>
public sealed class TenantAuthorizationResolverTests
{
    private static readonly Guid TenantA = new Guid("11111111-0000-0000-0000-0000000000a1");
    private static readonly Guid UserId = new Guid("22222222-0000-0000-0000-0000000000a1");

    private static readonly Guid TenantManagerRoleId = new Guid("33333333-0000-0000-0000-0000000000a1");
    private static readonly Guid TenantViewerRoleId = new Guid("33333333-0000-0000-0000-0000000000a2");
    private static readonly Guid PlatformAdminRoleId = new Guid("33333333-0000-0000-0000-0000000000a3");

    [Fact]
    public async Task ResolveAsync_GlobalOperationalRole_IsNotHonoredInTenant()
    {
        // Arrange — the user is TenantViewer in Tenant A (tenant-scoped) and, contrived, holds a GLOBAL TenantManager role.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var resolver = CreateResolver(scope.Context);
        await SetTenantRoleAsync(scope.Context, TestRoles.TenantViewer);

        // Act — compose effective authorization for Tenant A, passing a global operational role.
        var (roles, permissions) = await resolver.ResolveAsync(UserId, [TestRoles.TenantManager], TenantA, TestContext.Current.CancellationToken);

        // Assert — the global TenantManager is filtered out; only the tenant-scoped TenantViewer role is honored.
        roles.ShouldBe([TestRoles.TenantViewer]);

        // TenantViewer permissions are present; TenantManager-only permissions (members:manage) are NOT —
        // proving the global operational role conferred nothing inside the tenant.
        permissions.ShouldContain(TestPermissions.VehiclesRead);
        permissions.ShouldNotContain(TestPermissions.MembersManage);
    }

    [Fact]
    public async Task ResolveAsync_GlobalPlatformRole_IsHonoredAlongsideTenantRole()
    {
        // Arrange — the user is TenantViewer in Tenant A and holds the GLOBAL PlatformAdmin role.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var resolver = CreateResolver(scope.Context);
        await SetTenantRoleAsync(scope.Context, TestRoles.TenantViewer);

        // Act
        var (roles, permissions) = await resolver.ResolveAsync(UserId, [TestRoles.PlatformAdmin], TenantA, TestContext.Current.CancellationToken);

        // Assert — the platform-scope global role IS honored, unioned with the tenant role.
        roles.OrderBy(r => r).ToList().ShouldBe([TestRoles.PlatformAdmin, TestRoles.TenantViewer]);

        // Both the platform capability and the tenant-scoped operational permissions are present.
        permissions.ShouldContain(TestPermissions.TenantsManage);
        permissions.ShouldContain(TestPermissions.VehiclesRead);
    }

    [Fact]
    public async Task ResolveAsync_NoTenant_HonorsOnlyPlatformGlobalRoles()
    {
        // Arrange — a user signing in with no active tenant (invite-only onboarding) holding a global
        // operational role and the platform role. Only the platform role should be honored.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var resolver = CreateResolver(scope.Context);

        // Act — no tenant context.
        var (roles, permissions) = await resolver.ResolveAsync(UserId, [TestRoles.TenantManager, TestRoles.PlatformAdmin], tenantId: null, TestContext.Current.CancellationToken);

        // Assert — the global operational role is filtered; only the platform role survives, and it confers
        // only platform permissions (no tenant operational access).
        roles.ShouldBe([TestRoles.PlatformAdmin]);
        permissions.ShouldContain(TestPermissions.TenantsManage);
        permissions.ShouldNotContain(TestPermissions.MembersManage);
        permissions.ShouldNotContain(TestPermissions.VehiclesRead);
    }

    [Fact]
    public async Task ResolveAsync_TenantScopedPlatformRoleRow_IsDroppedDefensively()
    {
        // Arrange — a tenant-scoped PlatformAdmin assignment that bypassed the write-side rejection
        // (legacy data / manual SQL). It must not confer anything at resolution time.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        scope.Context.TenantMembershipRoles.Add(new BlueprintTenantMembershipRole<TestUser, TestTenant>
        {
            UserId = UserId,
            TenantId = TenantA,
            RoleId = PlatformAdminRoleId,
        });
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolver = CreateResolver(scope.Context);

        // Act
        var (roles, permissions) = await resolver.ResolveAsync(UserId, [], TenantA, TestContext.Current.CancellationToken);

        // Assert — the rogue tenant-scoped platform role is filtered out entirely.
        roles.ShouldBeEmpty();
        permissions.ShouldNotContain(TestPermissions.TenantsManage);
    }

    [Fact]
    public async Task ResolveAsync_NonGrantableDirectPermissionRow_IsDroppedDefensively()
    {
        // Arrange — a direct tenants:manage grant that bypassed the write-side rejection.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        scope.Context.TenantMembershipPermissions.Add(new BlueprintTenantMembershipPermission<TestUser, TestTenant>
        {
            UserId = UserId,
            TenantId = TenantA,
            Permission = TestPermissions.TenantsManage,
        });
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var resolver = CreateResolver(scope.Context);

        // Act
        var (_, permissions) = await resolver.ResolveAsync(UserId, [], TenantA, TestContext.Current.CancellationToken);

        // Assert — the platform-scope grant never reaches the effective permission set.
        permissions.ShouldNotContain(TestPermissions.TenantsManage);
    }

    private static TenantAuthorizationResolver CreateResolver(TestDbContext context)
    {
        var normalizer = new UpperInvariantLookupNormalizer();
        var rolePermissionResolver = new TestRolePermissionResolver();
        var roleService = new TenantRoleService<TestUser, TestTenant, TestDbContext>(context, normalizer, rolePermissionResolver);
        var permissionService = new TenantPermissionService<TestUser, TestTenant, TestDbContext>(context, rolePermissionResolver);
        return new TenantAuthorizationResolver(roleService, permissionService, rolePermissionResolver);
    }

    private static async Task SetTenantRoleAsync(TestDbContext context, string role)
    {
        var roleService = new TenantRoleService<TestUser, TestTenant, TestDbContext>(
            context,
            new UpperInvariantLookupNormalizer(),
            new TestRolePermissionResolver());
        await roleService.SetRolesAsync(UserId, TenantA, [role], TestContext.Current.CancellationToken);
    }

    private static async Task SeedRolesAsync(TestDbContext context)
    {
        await context.Roles.AddRangeAsync(
            CreateRole(TenantManagerRoleId, TestRoles.TenantManager),
            CreateRole(TenantViewerRoleId, TestRoles.TenantViewer),
            CreateRole(PlatformAdminRoleId, TestRoles.PlatformAdmin));

        // Tenant-scoped role/permission writes require an existing membership (the services verify it).
        await context.TenantMemberships.AddAsync(
            new BlueprintTenantMembership<TestUser, TestTenant> { UserId = UserId, TenantId = TenantA });

        await context.SaveChangesAsync();
    }

    private static TestRole CreateRole(Guid id, string name)
    {
        return new TestRole
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
        };
    }
}
