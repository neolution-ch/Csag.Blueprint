namespace Csag.Blueprint.Tests.Shared;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Smoke tests proving the shared test closures work: the <see cref="Database.TestDbContext"/> model
/// builds, the global tenant query filter closes over the ambient <see cref="TenantContext"/>, and
/// <see cref="TestRolePermissionResolver"/> exposes the expected platform/tenant scope split.
/// </summary>
public sealed class SharedClosuresSmokeTests : IDisposable
{
    private bool disposed;

    [Fact]
    public void CreateInMemoryDbContext_ModelBuilds_AndDiscoversTestVehicle()
    {
        // Arrange & Act — creating the context runs EnsureCreated, which builds the full model
        // (Identity, Blueprint configurations, and the multi-tenancy conventions).
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();

        // Assert
        scope.Context.Model.FindEntityType(typeof(TestVehicle)).ShouldNotBeNull();
        scope.Context.Model.FindEntityType(typeof(TestTenant)).ShouldNotBeNull();
        scope.Context.Model.FindEntityType(typeof(TestUser)).ShouldNotBeNull();
        scope.Context.Model.FindEntityType(typeof(TestRole)).ShouldNotBeNull();
    }

    [Fact]
    public void CreateInMemoryDbContext_WithTenantId_SetsAmbientTenantContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        using var scope = TestDbContextFactory.CreateInMemoryDbContext(tenantId);

        // Assert
        TenantContext.Current.ShouldBe(tenantId);
    }

    [Fact]
    public async Task Vehicles_SavedUnderTenantA_AreInvisibleUnderTenantB()
    {
        // Arrange — save a vehicle while tenant A is the ambient tenant.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var scope = TestDbContextFactory.CreateInMemoryDbContext(tenantA);
        scope.Context.Vehicles.Add(new TestVehicle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Cargo Bike",
            Kind = TestVehicleKind.Bicycle,
            Capacity = 2,
            PricePerHour = 12.50m,
            AcquiredAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert — under tenant B the row is filtered out by the global tenant query filter,
        // even though it exists in the store.
        TenantContext.SetTenant(tenantB);
        (await scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        (await scope.Context.Vehicles.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        // Back under tenant A the row is visible again — the filter re-evaluates per query.
        TenantContext.SetTenant(tenantA);
        (await scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public void GetPermissionsForRole_ReturnsExpectedScopeSplit()
    {
        // Arrange
        var resolver = new TestRolePermissionResolver();

        // Act & Assert — the read-only tenant role confers exactly the read permission.
        resolver.GetPermissionsForRole(TestRoles.TenantViewer).ShouldBe([TestPermissions.VehiclesRead]);

        // The managing tenant role confers all tenant-scope permissions and no platform permission.
        var managerPermissions = resolver.GetPermissionsForRole(TestRoles.TenantManager).ToList();
        managerPermissions.ShouldBe([TestPermissions.VehiclesRead, TestPermissions.VehiclesManage, TestPermissions.MembersManage], ignoreOrder: true);
        managerPermissions.ShouldNotContain(TestPermissions.TenantsManage);

        // The platform role confers exactly the platform-scope permissions and no tenant operational access.
        var platformPermissions = resolver.GetPermissionsForRole(TestRoles.PlatformAdmin).ToList();
        platformPermissions.ShouldBe([TestPermissions.TenantsManage]);
        platformPermissions.ShouldNotContain(TestPermissions.VehiclesRead);

        // Unknown roles confer nothing.
        resolver.GetPermissionsForRole("Bogus").ShouldBeEmpty();
    }

    [Fact]
    public void IsPlatformScopeRole_TrueOnlyForPlatformRoles()
    {
        // Arrange
        var resolver = new TestRolePermissionResolver();

        // Act & Assert
        resolver.IsPlatformScopeRole(TestRoles.PlatformAdmin).ShouldBeTrue();
        resolver.IsPlatformScopeRole(TestRoles.TenantViewer).ShouldBeFalse();
        resolver.IsPlatformScopeRole(TestRoles.TenantManager).ShouldBeFalse();
        resolver.IsPlatformScopeRole("Bogus").ShouldBeFalse();
    }

    [Fact]
    public void IsTenantGrantablePermission_TrueOnlyForTenantScopePermissions()
    {
        // Arrange
        var resolver = new TestRolePermissionResolver();

        // Act & Assert
        resolver.IsTenantGrantablePermission(TestPermissions.VehiclesRead).ShouldBeTrue();
        resolver.IsTenantGrantablePermission(TestPermissions.VehiclesManage).ShouldBeTrue();
        resolver.IsTenantGrantablePermission(TestPermissions.MembersManage).ShouldBeTrue();
        resolver.IsTenantGrantablePermission(TestPermissions.TenantsManage).ShouldBeFalse();
        resolver.IsTenantGrantablePermission("bogus:permission").ShouldBeFalse();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // Reset the ambient tenant so tests don't leak into one another.
        TenantContext.Clear();
        this.disposed = true;
    }
}
