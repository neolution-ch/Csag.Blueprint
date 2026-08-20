namespace Csag.Blueprint.Infrastructure.UnitTests.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Tenancy;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unit tests for <see cref="TenantPermissionService{TUser, TTenant, TContext}"/> covering tenant-scoped
/// direct permission grants, multi-permission membership, and per-tenant permission isolation.
/// </summary>
public sealed class TenantPermissionServiceTests
{
    private static readonly Guid TenantA = new Guid("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("11111111-0000-0000-0000-000000000002");
    private static readonly Guid UserId = new Guid("22222222-0000-0000-0000-000000000001");

    [Fact]
    public async Task SetPermissionsAsync_AssignsTenantScopedPermissions()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage], TestContext.Current.CancellationToken);

        var permissions = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        permissions.ShouldBe([TestPermissions.VehiclesManage]);
    }

    [Fact]
    public async Task SetPermissionsAsync_SupportsMultiplePermissionsPerTenant()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage, TestPermissions.MembersManage], TestContext.Current.CancellationToken);

        var permissions = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        permissions.ShouldBe(new[] { TestPermissions.VehiclesManage, TestPermissions.MembersManage }, ignoreOrder: true);
    }

    [Fact]
    public async Task SetPermissionsAsync_AllowsDifferentPermissionsInDifferentTenants()
    {
        // Headline capability: the same user holds a direct grant in Tenant A that does not leak to Tenant B.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage], TestContext.Current.CancellationToken);
        await service.SetPermissionsAsync(UserId, TenantB, [TestPermissions.MembersManage], TestContext.Current.CancellationToken);

        var permissionsInA = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        var permissionsInB = await service.GetPermissionNamesAsync(UserId, TenantB, TestContext.Current.CancellationToken);

        permissionsInA.ShouldBe([TestPermissions.VehiclesManage]);
        permissionsInB.ShouldBe([TestPermissions.MembersManage]);
    }

    [Fact]
    public async Task SetPermissionsAsync_IsIdempotentAndDiffsExistingGrants()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage, TestPermissions.MembersManage], TestContext.Current.CancellationToken);

        // Replace with a different set: VehiclesRead added, the previous two removed.
        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesRead], TestContext.Current.CancellationToken);

        var permissions = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        permissions.ShouldBe([TestPermissions.VehiclesRead]);

        // Exactly one row should remain for this (user, tenant).
        var rowCount = await scope.Context.TenantMembershipPermissions
            .CountAsync(mp => mp.UserId == UserId && mp.TenantId == TenantA, TestContext.Current.CancellationToken);
        rowCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetPermissionsAsync_RemovingAllPermissions_ClearsGrants()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage], TestContext.Current.CancellationToken);
        await service.SetPermissionsAsync(UserId, TenantA, [], TestContext.Current.CancellationToken);

        var permissions = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPermissionsAsync_NonTenantGrantablePermission_Throws()
    {
        // Platform-scope permissions (and unknown values) must never be persisted as direct grants —
        // a tenants:manage grant would confer a platform capability at auth time.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedMembershipsAsync(scope.Context);
        var service = CreateService(scope.Context);

        var ex = await Should.ThrowAsync<InvalidTenantAssignmentException>(
            () => service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.TenantsManage], TestContext.Current.CancellationToken));

        // InvalidName carries just the offending value — it is what endpoints echo to the client.
        ex.InvalidName.ShouldBe(TestPermissions.TenantsManage);
        ex.Message.ShouldContain(TestPermissions.TenantsManage);

        var permissions = await service.GetPermissionNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPermissionsAsync_WithoutMembership_Throws()
    {
        // Direct grants hang off the membership; granting them without one must fail with a clear
        // error instead of a raw FK violation. Mirrors the membership check in TenantRoleService.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var service = CreateService(scope.Context);

        var ex = await Should.ThrowAsync<TenantMembershipRequiredException>(
            () => service.SetPermissionsAsync(UserId, TenantA, [TestPermissions.VehiclesManage], TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not a member");
    }

    private static TenantPermissionService<TestUser, TestTenant, TestDbContext> CreateService(TestDbContext context)
    {
        return new TenantPermissionService<TestUser, TestTenant, TestDbContext>(
            context,
            new TestRolePermissionResolver());
    }

    private static async Task SeedMembershipsAsync(TestDbContext context)
    {
        // Tenant-scoped permission grants require an existing membership (the service verifies it).
        await context.TenantMemberships.AddRangeAsync(
            new BlueprintTenantMembership<TestUser, TestTenant> { UserId = UserId, TenantId = TenantA },
            new BlueprintTenantMembership<TestUser, TestTenant> { UserId = UserId, TenantId = TenantB });

        await context.SaveChangesAsync();
    }
}
