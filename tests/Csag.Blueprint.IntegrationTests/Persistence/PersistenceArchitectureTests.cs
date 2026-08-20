namespace Csag.Blueprint.IntegrationTests.Persistence;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for the shared persistence rules applied through <see cref="TestDbContext"/>
/// against real SQL Server: the global tenant query filter (per-ambient-tenant isolation, the
/// fail-closed no-tenant case, and the <c>IgnoreQueryFilters</c> escape hatch), tenant enforcement
/// during SaveChanges (auto-stamping on insert, immutability on update, rejection without an
/// ambient tenant), and role persistence through ASP.NET Core Identity over the Blueprint base types.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class PersistenceArchitectureTests(AppFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public void TestDbContext_IsBlueprintDbContextClosure_ExposingAmbientCurrentTenantId()
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);

        // The concrete context runs through the Blueprint base context that owns CurrentTenantId
        // and the shared multi-tenancy wiring.
        scope.Context.ShouldBeAssignableTo<BlueprintDbContext<TestTenant, TestUser, TestRole>>();
        scope.Context.CurrentTenantId.ShouldBe(SeedData.TenantAId);

        // CurrentTenantId reads the ambient tenant per access, so switching the ambient tenant is
        // observable on the same context instance.
        TenantContext.SetTenant(SeedData.TenantBId);
        scope.Context.CurrentTenantId.ShouldBe(SeedData.TenantBId);
    }

    [Fact]
    public async Task TestDbContext_QueryFilters_IsolateTenantDataPerAmbientTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);

        // Query the same context under two different ambient tenants. This guards that the global
        // filter is evaluated per query using the current ambient tenant.
        var totalVehicleCount = await scope.Context.Vehicles
            .IgnoreQueryFilters()
            .CountAsync(ct);

        var tenantAVehicleIds = await scope.Context.Vehicles
            .Select(v => v.Id)
            .ToListAsync(ct);

        TenantContext.SetTenant(SeedData.TenantBId);
        var tenantBVehicleIds = await scope.Context.Vehicles
            .Select(v => v.Id)
            .ToListAsync(ct);

        totalVehicleCount.ShouldBe(
            SeedData.TenantAVehicleCount + SeedData.TenantBVehicleCount,
            "The seeded dataset should contain all tenant rows when filters are bypassed");
        tenantAVehicleIds.Count.ShouldBe(SeedData.TenantAVehicleCount, "Tenant A should only see its own seeded vehicles");
        tenantBVehicleIds.Count.ShouldBe(SeedData.TenantBVehicleCount, "Tenant B should only see its own seeded vehicles");
        tenantAVehicleIds.ShouldNotContain(id => tenantBVehicleIds.Contains(id), "Tenant-scoped query filter should prevent cross-tenant overlap");
    }

    [Fact]
    public async Task TestDbContext_SaveInterceptor_StampsTenantOnInsertFromAmbientTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope(SeedData.TenantBId);

        // Deliberately provide the wrong TenantId to prove inserts are stamped from the ambient
        // tenant context, not from caller-supplied values.
        var vehicle = new TestVehicle
        {
            Id = Guid.NewGuid(),
            Name = "Interceptor stamped vehicle",
            Kind = TestVehicleKind.Scooter,
            Capacity = 2,
            PricePerHour = 12.50m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            TenantId = SeedData.TenantAId,
        };

        scope.Context.Vehicles.Add(vehicle);
        await scope.Context.SaveChangesAsync(ct);

        vehicle.TenantId.ShouldBe(SeedData.TenantBId, "TenantSaveInterceptor should stamp the ambient tenant on insert");

        // Re-read from the database so the assertion validates persisted state, not only tracked state.
        scope.Context.ChangeTracker.Clear();
        var persisted = await scope.Context.Vehicles.SingleAsync(v => v.Id == vehicle.Id, ct);

        persisted.TenantId.ShouldBe(SeedData.TenantBId);
    }

    [Fact]
    public async Task TestDbContext_SaveInterceptor_RejectsTenantMutationOnExistingEntityAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);

        // Attempt to move an existing tenant-owned row across tenants.
        // This must fail because tenant ownership is immutable after insert.
        var vehicle = await scope.Context.Vehicles.OrderBy(v => v.Name).FirstAsync(ct);
        vehicle.TenantId = SeedData.TenantBId;

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => scope.Context.SaveChangesAsync(ct));

        exception.Message.ShouldContain("Cannot modify TenantId");
    }

    [Fact]
    public async Task TestDbContext_SaveInterceptor_AllowsBusinessUpdatesWhileStillBlockingTenantMutationAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);

        // Update a normal business field first to prove regular modifications still persist.
        var vehicle = await scope.Context.Vehicles.OrderBy(v => v.Name).FirstAsync(ct);

        var originalTenantId = vehicle.TenantId;
        var updatedName = $"{vehicle.Name} - renamed";

        vehicle.Name = updatedName;
        await scope.Context.SaveChangesAsync(ct);

        scope.Context.ChangeTracker.Clear();
        var persisted = await scope.Context.Vehicles.SingleAsync(v => v.Id == vehicle.Id, ct);

        persisted.Name.ShouldBe(updatedName, "Normal business-property updates should continue to work for tenant-owned entities");
        persisted.TenantId.ShouldBe(originalTenantId, "A non-tenant-property update must not alter tenant ownership");

        // Then attempt a tenant move on the same entity to prove the guard still applies.
        persisted.TenantId = SeedData.TenantBId;
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => scope.Context.SaveChangesAsync(ct));

        exception.Message.ShouldContain("Cannot modify TenantId");
    }

    [Fact]
    public async Task TestDbContext_QueryFilters_FailClosedWhenAmbientTenantIsMissingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // No tenant argument — the scope clears the ambient tenant context.
        using var scope = this.App.CreateDbContextScope();

        // Sanity-check the seeded dataset so this test proves fail-closed behavior,
        // not an accidentally empty table.
        var totalVehicleCountIgnoringFilters = await scope.Context.Vehicles
            .IgnoreQueryFilters()
            .CountAsync(ct);

        totalVehicleCountIgnoringFilters.ShouldBeGreaterThan(0, "The seeded dataset should contain tenant-owned rows so fail-closed behavior is meaningful");

        // Without an ambient tenant the filter compares TenantId against a null parameter in the
        // lifted nullable form — SQL "TenantId = NULL" matches nothing — so the query returns no
        // rows deterministically instead of leaking any tenant's data.
        var visibleVehicleIds = await scope.Context.Vehicles.Select(v => v.Id).ToListAsync(ct);

        visibleVehicleIds.ShouldBeEmpty("Tenant-owned rows must be invisible when no ambient tenant is set");
    }

    [Fact]
    public async Task TestDbContext_SaveInterceptor_RejectsInsertWhenAmbientTenantIsMissingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope();

        // Attempt to insert a tenant-owned entity without ambient tenant context.
        // The interceptor must reject this instead of accepting the caller-supplied TenantId.
        scope.Context.Vehicles.Add(new TestVehicle
        {
            Id = Guid.NewGuid(),
            Name = "Missing ambient tenant vehicle",
            Kind = TestVehicleKind.Kayak,
            Capacity = 4,
            PricePerHour = 18.75m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            TenantId = Guid.NewGuid(),
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => scope.Context.SaveChangesAsync(ct));

        exception.Message.ShouldContain("without a valid tenant");
    }

    [Fact]
    public async Task TestRole_PersistsAsBlueprintRoleWithIdentityRoundTripAndAuditTimestampsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<TestRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var roleName = $"PersistenceRole-{Guid.NewGuid():N}";
        var userEmail = $"role-roundtrip-{Guid.NewGuid():N}@test.local";

        // Create the concrete role through Identity to prove runtime APIs work with the shared base type.
        var createRoleResult = await roleManager.CreateAsync(new TestRole
        {
            Name = roleName,
        });

        createRoleResult.Succeeded.ShouldBeTrue(string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));

        var persistedRole = await context.Roles.SingleAsync(r => r.Name == roleName, ct);

        // The role must materialize as the concrete type and still carry the shared persistence fields.
        persistedRole.ShouldBeOfType<TestRole>();
        persistedRole.ShouldBeAssignableTo<BlueprintRole>();
        persistedRole.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
        persistedRole.UpdatedAt.ShouldBeNull();

        var user = new TestUser
        {
            Email = userEmail,
            UserName = userEmail,
            EmailConfirmed = true,
        };

        var createUserResult = await userManager.CreateAsync(user, SeedData.DefaultPassword);
        createUserResult.Succeeded.ShouldBeTrue(string.Join(", ", createUserResult.Errors.Select(e => e.Description)));

        // Round-trip the role through user assignment to prove it participates in normal Identity resolution.
        var addToRoleResult = await userManager.AddToRoleAsync(user, roleName);
        addToRoleResult.Succeeded.ShouldBeTrue(string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));

        var assignedRoles = await userManager.GetRolesAsync(user);
        assignedRoles.ShouldContain(roleName, "A persisted TestRole should participate in normal ASP.NET Identity role assignment");
    }
}
