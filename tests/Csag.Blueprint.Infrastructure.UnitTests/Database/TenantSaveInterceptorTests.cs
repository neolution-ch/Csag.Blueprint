namespace Csag.Blueprint.Infrastructure.UnitTests.Database;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;

/// <summary>
/// Unit tests for <see cref="TenantSaveInterceptor"/> verifying the tenancy write-path enforcement:
/// added entities get their <c>TenantId</c> stamped from the ambient <see cref="TenantContext"/>
/// (fail-closed when no tenant is set), and moving an existing entity between tenants is rejected.
/// </summary>
public sealed class TenantSaveInterceptorTests : IDisposable
{
    private static readonly Guid OtherTenantId = new Guid("00000000-0000-0000-0000-000000000002");

    private bool disposed;

    [Fact]
    public async Task SavingChanges_OnInsert_AssignsTenantIdFromAmbientTenant()
    {
        // Arrange — the factory sets TenantContext.Current to TestTenantId before creating the context.
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.TenantId.ShouldBe(TestDbContextFactory.TestTenantId);
    }

    [Fact]
    public async Task SavingChanges_OnInsertWithExplicitForeignTenantId_SilentlyOverwritesWithAmbientTenant()
    {
        // Arrange — the caller pre-sets a different tenant's ID on the new entity.
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();
        vehicle.TenantId = OtherTenantId;

        // Act
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — the interceptor overwrites unconditionally instead of throwing, so a caller can
        // never smuggle a row into another tenant by pre-setting TenantId.
        vehicle.TenantId.ShouldBe(TestDbContextFactory.TestTenantId);
    }

    [Fact]
    public async Task SavingChanges_OnInsertWithNoAmbientTenant_Throws()
    {
        // Arrange — clear the ambient tenant AFTER the factory set it, simulating a save outside
        // any tenant scope (e.g. a background job that forgot to establish one).
        using var scope = CreateContextScope();
        TenantContext.Clear();
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert — fail-closed: no ambient tenant means the insert is rejected, not saved tenant-less.
        exception.Message.ShouldContain(nameof(TestVehicle));
        exception.Message.ShouldContain("without a valid tenant");
    }

    [Fact]
    public async Task SavingChanges_OnUpdate_TenantIdModificationThrows()
    {
        // Arrange
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — attempt to move the tracked entity to another tenant.
        vehicle.TenantId = OtherTenantId;
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert — the mechanism is a hard throw (not a silent revert of the property).
        exception.Message.ShouldContain("Cannot modify TenantId");
        exception.Message.ShouldContain(nameof(TestVehicle));
    }

    [Fact]
    public async Task SavingChanges_OnUpdateOfOtherProperties_SucceedsAndKeepsTenantId()
    {
        // Arrange
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — a regular business edit that leaves TenantId untouched.
        vehicle.Name = "Renamed";
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — the modification guard only fires when TenantId itself changes.
        vehicle.TenantId.ShouldBe(TestDbContextFactory.TestTenantId);
    }

    [Fact]
    public async Task SavingChanges_OnNonTenantEntity_IsIgnoredEvenWithoutAmbientTenant()
    {
        // Arrange — tenants themselves don't implement IMustHaveTenant, so the interceptor
        // must not touch them or require an ambient tenant to save them.
        using var scope = CreateContextScope();
        TenantContext.Clear();
        var tenant = new TestTenant { Id = Guid.NewGuid(), Name = "Tenant Zero" };

        // Act
        scope.Context.Add(tenant);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        scope.Context.Tenants.Local.ShouldContain(tenant);
    }

    [Fact]
    public void SavingChanges_SynchronousSave_AssignsTenantIdFromAmbientTenant()
    {
        // Arrange — the interceptor overrides both the async and the sync save hook, and this
        // covers the synchronous one.
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        scope.Context.SaveChanges();

        // Assert
        vehicle.TenantId.ShouldBe(TestDbContextFactory.TestTenantId);
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

    private static TestDbContextScope<TestDbContext> CreateContextScope()
    {
        // Register the interceptor under test on the context options so SaveChanges triggers it,
        // mirroring the production registration on the pooled DbContext options. The factory also
        // sets the ambient tenant the interceptor reads for IMustHaveTenant assignment.
        return TestDbContextFactory.CreateInMemoryDbContext(new TenantSaveInterceptor());
    }

    private static TestVehicle CreateVehicle() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Vehicle",
        Kind = TestVehicleKind.Bicycle,
        Capacity = 2,
        PricePerHour = 20m,
    };
}
