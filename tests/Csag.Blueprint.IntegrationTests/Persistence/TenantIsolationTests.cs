namespace Csag.Blueprint.IntegrationTests.Persistence;

using System.Net;
using System.Net.Http.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Vehicles;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Integration tests for multi-tenant data isolation through the HTTP surface.
/// Verifies that tenants cannot access or modify data belonging to other tenants: the EF Core
/// global query filter over the ambient tenant (set by the tenant middleware from the session's
/// TenantId claim) makes foreign rows indistinguishable from missing ones.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TenantIsolationTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task TenantA_CannotQueryTenantBDataAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — each tenant's manager lists all vehicles of its active tenant.
        var tenantAVehicles = await ListVehiclesAsync(this.App.ManagerAClient, ct);
        var tenantBVehicles = await ListVehiclesAsync(this.App.ManagerBClient, ct);

        // Assert — each tenant sees only its own seeded data.
        tenantAVehicles.Count.ShouldBe(SeedData.TenantAVehicleCount, "Tenant A should see only its own seeded vehicles");
        tenantBVehicles.Count.ShouldBe(SeedData.TenantBVehicleCount, "Tenant B should see only its own seeded vehicles");

        // Assert — no vehicle IDs overlap between tenants.
        var tenantAIds = tenantAVehicles.Select(v => v.Id).ToList();
        var tenantBIds = tenantBVehicles.Select(v => v.Id).ToList();

        tenantAIds.ShouldNotContain(id => tenantBIds.Contains(id), "Tenant A should not see Tenant B's vehicle IDs");
        tenantBIds.ShouldNotContain(id => tenantAIds.Contains(id), "Tenant B should not see Tenant A's vehicle IDs");
    }

    [Fact]
    public async Task TenantA_CannotGetTenantBVehicleByIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — get a vehicle ID from Tenant B.
        var tenantBVehicleId = (await ListVehiclesAsync(this.App.ManagerBClient, ct)).First().Id;

        // Act — Tenant A tries to get Tenant B's vehicle by ID.
        var response = await this.App.ManagerAClient.GetAsync(VehicleUri(tenantBVehicleId), ct);

        // Assert — the query filter hides the row, so the endpoint reports 404 rather than 403,
        // leaking nothing about the row's existence in another tenant.
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, "Tenant A should not be able to see Tenant B's vehicle", ct);
    }

    [Fact]
    public async Task TenantB_CanAccessTheirOwnVehicleByIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — get a vehicle ID from Tenant B.
        var tenantBVehicleId = (await ListVehiclesAsync(this.App.ManagerBClient, ct)).First().Id;

        // Act — Tenant B gets its own vehicle.
        var response = await this.App.ManagerBClient.GetAsync(VehicleUri(tenantBVehicleId), ct);

        // Assert — Tenant B can access its own data.
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        var vehicle = await response.Content.ReadFromJsonAsync<VehicleResponse>(BlueprintJsonOptions.Default, ct);
        vehicle.ShouldNotBeNull();
        vehicle.Id.ShouldBe(tenantBVehicleId);
    }

    [Fact]
    public async Task TenantA_CannotDeleteTenantBVehicleAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — get a vehicle ID from Tenant B.
        var tenantBVehicleId = (await ListVehiclesAsync(this.App.ManagerBClient, ct)).First().Id;

        // Act — Tenant A tries to delete Tenant B's vehicle.
        var deleteResponse = await this.App.ManagerAClient.DeleteAsync(VehicleUri(tenantBVehicleId), ct);

        // Assert — the query filter prevents access, so the endpoint reports 404.
        await deleteResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, "Tenant A should not be able to delete Tenant B's vehicle", ct);

        // Assert — Tenant B's vehicle still exists.
        var verifyResponse = await this.App.ManagerBClient.GetAsync(VehicleUri(tenantBVehicleId), ct);
        await verifyResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "Tenant B's vehicle should still exist", ct);
    }

    [Fact]
    public async Task TenantB_CanDeleteTheirOwnVehicleAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — create a fresh vehicle for Tenant B so the seeded rows stay untouched.
        var createResponse = await this.App.ManagerBClient.PostAsJsonAsync(
            VehiclesUri,
            new CreateVehicleRequest
            {
                Name = "Tenant B Temp Scooter",
                Kind = TestVehicleKind.Scooter,
                Capacity = 1,
                PricePerHour = 10.00m,
                IsActive = true,
                AcquiredAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            BlueprintJsonOptions.Default,
            ct);

        await createResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, cancellationToken: ct);
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>(BlueprintJsonOptions.Default, ct);
        created.ShouldNotBeNull();

        // Act — Tenant B deletes its own vehicle.
        var deleteResponse = await this.App.ManagerBClient.DeleteAsync(VehicleUri(created.Id), ct);

        // Assert — Tenant B can delete its own data.
        await deleteResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.NoContent, cancellationToken: ct);

        // Assert — the vehicle is actually gone.
        var verifyResponse = await this.App.ManagerBClient.GetAsync(VehicleUri(created.Id), ct);
        await verifyResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, "Deleted vehicle should not be found", ct);
    }

    [Fact]
    public async Task CreateVehicle_AsTenantA_IsStampedWithTenantAAndHiddenFromTenantBAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — Tenant A creates a new vehicle.
        var createResponse = await this.App.ManagerAClient.PostAsJsonAsync(
            VehiclesUri,
            new CreateVehicleRequest
            {
                Name = "Isolation Test Bike",
                Kind = TestVehicleKind.Bicycle,
                Capacity = 1,
                PricePerHour = 7.50m,
                IsActive = true,
                AcquiredAt = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            BlueprintJsonOptions.Default,
            ct);

        await createResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, cancellationToken: ct);
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>(BlueprintJsonOptions.Default, ct);
        created.ShouldNotBeNull();

        // Assert — the persisted row carries Tenant A's ID, stamped by the save interceptor from
        // the ambient tenant of the authenticated request.
        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            var persisted = await scope.Context.Vehicles.SingleAsync(v => v.Id == created.Id, ct);
            persisted.TenantId.ShouldBe(SeedData.TenantAId, "The save interceptor should stamp the creator's active tenant");
        }

        // Assert — Tenant A can read the new vehicle back.
        var readResponse = await this.App.ManagerAClient.GetAsync(VehicleUri(created.Id), ct);
        await readResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "Tenant A should see its own newly created vehicle", ct);

        // Assert — Tenant B cannot see the new vehicle.
        var crossTenantResponse = await this.App.ManagerBClient.GetAsync(VehicleUri(created.Id), ct);
        await crossTenantResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, "Tenant B should not see Tenant A's newly created vehicle", ct);

        // Assert — Tenant B's vehicle count is unchanged.
        var tenantBVehicles = await ListVehiclesAsync(this.App.ManagerBClient, ct);
        tenantBVehicles.Count.ShouldBe(SeedData.TenantBVehicleCount, "Tenant B's vehicle count should not change when Tenant A creates a vehicle");
    }

    private static Uri VehicleUri(Guid id) => new($"/api/vehicles/{id}", UriKind.Relative);

    private static async Task<List<VehicleResponse>> ListVehiclesAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>(BlueprintJsonOptions.Default, ct);
        vehicles.ShouldNotBeNull();
        return vehicles;
    }
}
