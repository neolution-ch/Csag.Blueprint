namespace Csag.Blueprint.Infrastructure.UnitTests.Database;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;

/// <summary>
/// Unit tests for <see cref="AuditableTimestampInterceptor"/> verifying that it stamps
/// <c>CreatedByActor</c> / <c>UpdatedByActor</c> (and the timestamps) from the ambient
/// <see cref="CurrentActorContext"/>, and leaves the actor columns null when there is no acting actor.
/// </summary>
public sealed class AuditableTimestampInterceptorTests : IDisposable
{
    private bool disposed;

    [Fact]
    public async Task SavingChanges_OnInsert_StampsCreatedByActorFromCurrentActor()
    {
        // Arrange
        const string actor = "alice@example.com";
        CurrentActorContext.SetActor(actor);
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.CreatedByActor.ShouldBe(actor);
        vehicle.CreatedAt.ShouldNotBe(default);

        // Mirror of the timestamp logic: only Created* is set on insert.
        vehicle.UpdatedByActor.ShouldBeNull();
        vehicle.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SavingChanges_OnUpdate_StampsUpdatedByActorAndLeavesCreatedByActor()
    {
        // Arrange
        const string creator = "alice@example.com";
        const string editor = "sa-test-tenant-a"; // a service account edits the row
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();

        CurrentActorContext.SetActor(creator);
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: a different actor edits the row.
        CurrentActorContext.SetActor(editor);
        vehicle.Name = "Renamed";
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.CreatedByActor.ShouldBe(creator); // never overwritten on update
        vehicle.UpdatedByActor.ShouldBe(editor);
        vehicle.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SavingChanges_WithNoCurrentActor_LeavesCreatedByActorAndUpdatedByActorNull()
    {
        // Arrange: no acting actor (e.g. seeding / background service / migration).
        CurrentActorContext.Clear();
        using var scope = CreateContextScope();
        var vehicle = CreateVehicle();

        // Act: insert then update, both without a current actor.
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        vehicle.Name = "Renamed";
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert: actor columns stay null, timestamps still set (must not throw).
        vehicle.CreatedByActor.ShouldBeNull();
        vehicle.UpdatedByActor.ShouldBeNull();
        vehicle.CreatedAt.ShouldNotBe(default);
        vehicle.UpdatedAt.ShouldNotBeNull();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // Reset ambient contexts so tests don't leak into one another.
        CurrentActorContext.Clear();
        TenantContext.Clear();
        this.disposed = true;
    }

    private static TestDbContextScope<TestDbContext> CreateContextScope()
    {
        // Register the interceptor under test on the context options so SaveChanges triggers it,
        // mirroring the production registration on the pooled DbContext options. The factory also
        // sets the ambient tenant the query filter / IMustHaveTenant assignment needs.
        return TestDbContextFactory.CreateInMemoryDbContext(new AuditableTimestampInterceptor());
    }

    private static TestVehicle CreateVehicle() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TestDbContextFactory.TestTenantId,
        Name = "Test Vehicle",
        Kind = TestVehicleKind.Bicycle,
        Capacity = 2,
        PricePerHour = 20m,
    };
}
