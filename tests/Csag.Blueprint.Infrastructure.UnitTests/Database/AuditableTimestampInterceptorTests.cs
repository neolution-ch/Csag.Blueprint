namespace Csag.Blueprint.Infrastructure.UnitTests.Database;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Unit tests for <see cref="AuditableTimestampInterceptor"/> verifying that it stamps
/// <c>CreatedByActor</c> / <c>UpdatedByActor</c> from the ambient <see cref="CurrentActorContext"/>,
/// leaves the actor columns null when there is no acting actor, and reads every timestamp from the
/// injected <see cref="TimeProvider"/> rather than the wall clock.
/// </summary>
public sealed class AuditableTimestampInterceptorTests : IDisposable
{
    private static readonly DateTimeOffset CreationTime = new(2026, 3, 14, 9, 15, 0, TimeSpan.Zero);

    private bool disposed;

    [Fact]
    public async Task SavingChanges_OnInsert_StampsCreatedByActorFromCurrentActor()
    {
        // Arrange
        const string actor = "alice@example.com";
        CurrentActorContext.SetActor(actor);
        var clock = new FakeTimeProvider(CreationTime);
        using var scope = CreateContextScope(clock);
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.CreatedByActor.ShouldBe(actor);
        vehicle.CreatedAt.ShouldBe(CreationTime);

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
        var clock = new FakeTimeProvider(CreationTime);
        using var scope = CreateContextScope(clock);
        var vehicle = CreateVehicle();

        CurrentActorContext.SetActor(creator);
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: time passes, then a different actor edits the row.
        var elapsed = TimeSpan.FromMinutes(90);
        clock.Advance(elapsed);
        CurrentActorContext.SetActor(editor);
        vehicle.Name = "Renamed";
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.CreatedByActor.ShouldBe(creator); // never overwritten on update
        vehicle.UpdatedByActor.ShouldBe(editor);

        // The update reads the clock again: CreatedAt keeps the original instant while UpdatedAt
        // moves forward by exactly the elapsed time.
        vehicle.CreatedAt.ShouldBe(CreationTime);
        vehicle.UpdatedAt.ShouldBe(CreationTime + elapsed);
    }

    [Fact]
    public async Task SavingChanges_WithNoCurrentActor_LeavesCreatedByActorAndUpdatedByActorNull()
    {
        // Arrange: no acting actor (e.g. seeding / background service / migration).
        CurrentActorContext.Clear();
        var clock = new FakeTimeProvider(CreationTime);
        using var scope = CreateContextScope(clock);
        var vehicle = CreateVehicle();

        // Act: insert then update, both without a current actor.
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(5));
        vehicle.Name = "Renamed";
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert: actor columns stay null, timestamps still set (must not throw).
        vehicle.CreatedByActor.ShouldBeNull();
        vehicle.UpdatedByActor.ShouldBeNull();
        vehicle.CreatedAt.ShouldBe(CreationTime);
        vehicle.UpdatedAt.ShouldBe(CreationTime.AddMinutes(5));
    }

    [Fact]
    public async Task SavingChanges_StampsTheInjectedClockNotTheWallClock()
    {
        // Arrange: a clock set far from "now" — a wall-clock read could not produce this value.
        var backdated = new DateTimeOffset(1999, 12, 31, 23, 59, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(backdated);
        using var scope = CreateContextScope(clock);
        var vehicle = CreateVehicle();

        // Act
        scope.Context.Add(vehicle);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        vehicle.CreatedAt.ShouldBe(backdated);
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

    private static TestDbContextScope<TestDbContext> CreateContextScope(TimeProvider timeProvider)
    {
        // Register the interceptor under test on the context options so SaveChanges triggers it,
        // mirroring the production registration on the pooled DbContext options. The factory also
        // sets the ambient tenant the query filter / IMustHaveTenant assignment needs.
        return TestDbContextFactory.CreateInMemoryDbContext(new AuditableTimestampInterceptor(timeProvider));
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
