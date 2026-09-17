namespace Csag.Blueprint.Infrastructure.UnitTests.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Tenancy;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unit tests for <see cref="TenantRoleService{TUser, TTenant, TContext}"/> covering tenant-scoped
/// role assignment, multi-role membership, and per-tenant role isolation.
/// </summary>
public sealed class TenantRoleServiceTests
{
    private static readonly Guid TenantA = new Guid("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("11111111-0000-0000-0000-000000000002");
    private static readonly Guid UserId = new Guid("22222222-0000-0000-0000-000000000001");

    private static readonly Guid TenantViewerRoleId = new Guid("33333333-0000-0000-0000-000000000001");
    private static readonly Guid TenantManagerRoleId = new Guid("33333333-0000-0000-0000-000000000002");
    private static readonly Guid PlatformAdminRoleId = new Guid("33333333-0000-0000-0000-000000000003");

    [Fact]
    public async Task SetRolesAsync_AssignsTenantScopedRoles()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantManager], TestContext.Current.CancellationToken);

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.ShouldBe([TestRoles.TenantManager]);
    }

    [Fact]
    public async Task SetRolesAsync_SupportsMultipleRolesPerTenant()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantManager, TestRoles.TenantViewer], TestContext.Current.CancellationToken);

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.OrderBy(r => r).ToList().ShouldBe([TestRoles.TenantManager, TestRoles.TenantViewer]);
    }

    [Fact]
    public async Task SetRolesAsync_AllowsDifferentRolesInDifferentTenants()
    {
        // This is the headline capability: the same user is TenantManager in Tenant A but TenantViewer in Tenant B.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantManager], TestContext.Current.CancellationToken);
        await service.SetRolesAsync(UserId, TenantB, [TestRoles.TenantViewer], TestContext.Current.CancellationToken);

        var rolesInA = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        var rolesInB = await service.GetRoleNamesAsync(UserId, TenantB, TestContext.Current.CancellationToken);

        rolesInA.ShouldBe([TestRoles.TenantManager]);
        rolesInB.ShouldBe([TestRoles.TenantViewer]);
    }

    [Fact]
    public async Task SetRolesAsync_IsIdempotentAndDiffsExistingAssignments()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantManager, TestRoles.TenantViewer], TestContext.Current.CancellationToken);

        // Replace with a different set: TenantManager removed, TenantViewer kept (not duplicated).
        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantViewer], TestContext.Current.CancellationToken);

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.ShouldBe([TestRoles.TenantViewer]);

        // Exactly one row should remain for this (user, tenant).
        var rowCount = await scope.Context.TenantMembershipRoles
            .CountAsync(mr => mr.UserId == UserId && mr.TenantId == TenantA, TestContext.Current.CancellationToken);
        rowCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetRolesAsync_RemovingAllRoles_ClearsAssignments()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        await service.SetRolesAsync(UserId, TenantA, [TestRoles.TenantManager], TestContext.Current.CancellationToken);
        await service.SetRolesAsync(UserId, TenantA, [], TestContext.Current.CancellationToken);

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetRolesAsync_UnknownRole_Throws()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        var ex = await Should.ThrowAsync<InvalidTenantAssignmentException>(
            () => service.SetRolesAsync(UserId, TenantA, ["NotARealRole"], TestContext.Current.CancellationToken));

        // InvalidName carries just the offending value — it is what endpoints echo to the client.
        ex.InvalidName.ShouldBe("NotARealRole");
        ex.Message.ShouldContain("NotARealRole");
    }

    [Fact]
    public async Task SetRolesAsync_PlatformScopeRole_Throws()
    {
        // Platform-scope roles are assigned globally and must never be persisted as tenant-scoped
        // assignments — a tenant-scoped PlatformAdmin row would confer tenants:manage at auth time.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        var ex = await Should.ThrowAsync<InvalidTenantAssignmentException>(
            () => service.SetRolesAsync(UserId, TenantA, [TestRoles.PlatformAdmin], TestContext.Current.CancellationToken));

        ex.InvalidName.ShouldBe(TestRoles.PlatformAdmin);
        ex.Message.ShouldContain(TestRoles.PlatformAdmin);

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetRolesAsync_PlatformScopeRoleCaseVariant_Throws()
    {
        // The catalog lookup is case-insensitive (normalized names), so the platform-scope guard must
        // run on the resolved canonical name — "PLATFORMADMIN" must not slip past a case-sensitive
        // check and get persisted as a tenant-scoped PlatformAdmin assignment.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        var ex = await Should.ThrowAsync<InvalidTenantAssignmentException>(
            () => service.SetRolesAsync(UserId, TenantA, [TestRoles.PlatformAdmin.ToUpperInvariant()], TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("platform-scope");

        var roles = await service.GetRoleNamesAsync(UserId, TenantA, TestContext.Current.CancellationToken);
        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetRolesAsync_WithoutMembership_Throws()
    {
        // Tenant-scoped roles hang off the membership; assigning them without one must fail with a
        // clear error instead of a raw FK violation (or, worse, silently orphaned rows).
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedRolesAsync(scope.Context);
        var service = CreateService(scope.Context);

        var nonMemberTenant = new Guid("11111111-0000-0000-0000-00000000000f");

        var ex = await Should.ThrowAsync<TenantMembershipRequiredException>(
            () => service.SetRolesAsync(UserId, nonMemberTenant, [TestRoles.TenantManager], TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not a member");
    }

    private static TenantRoleService<TestUser, TestTenant, TestDbContext> CreateService(TestDbContext context)
    {
        return new TenantRoleService<TestUser, TestTenant, TestDbContext>(
            context,
            new UpperInvariantLookupNormalizer(),
            new TestRolePermissionResolver());
    }

    private static async Task SeedRolesAsync(TestDbContext context)
    {
        await context.Roles.AddRangeAsync(
            CreateRole(TenantViewerRoleId, TestRoles.TenantViewer),
            CreateRole(TenantManagerRoleId, TestRoles.TenantManager),
            CreateRole(PlatformAdminRoleId, TestRoles.PlatformAdmin));

        // Tenant-scoped role assignments require an existing membership (the service verifies it).
        await context.TenantMemberships.AddRangeAsync(
            new BlueprintTenantMembership<TestUser, TestTenant> { UserId = UserId, TenantId = TenantA },
            new BlueprintTenantMembership<TestUser, TestTenant> { UserId = UserId, TenantId = TenantB });

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
