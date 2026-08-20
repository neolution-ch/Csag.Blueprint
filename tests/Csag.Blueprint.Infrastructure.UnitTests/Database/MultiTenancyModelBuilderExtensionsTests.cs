namespace Csag.Blueprint.Infrastructure.UnitTests.Database;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unit tests for <see cref="MultiTenancyModelBuilderExtensions.ConfigureBlueprintMultiTenancy"/>
/// covering the global tenant query filter (including the fail-closed no-tenant case and the
/// <c>IgnoreQueryFilters</c> escape hatch), the tenant foreign key / index conventions, the
/// <c>addTenantForeignKey: false</c> split-plane path, and the missing-property guard.
/// </summary>
public sealed class MultiTenancyModelBuilderExtensionsTests : IDisposable
{
    private static readonly Guid TenantA = new Guid("44444444-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("44444444-0000-0000-0000-000000000002");

    private bool disposed;

    [Fact]
    public async Task QueryFilter_HidesRowsBelongingToOtherTenants()
    {
        // Arrange — seed a row owned by tenant A.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext(TenantA);
        scope.Context.Vehicles.Add(CreateVehicle(TenantA));
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert — the filter re-reads the ambient tenant per query, so switching the
        // ambient tenant flips visibility on the same context instance.
        (await scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        TenantContext.SetTenant(TenantB);
        (await scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);

        TenantContext.SetTenant(TenantA);
        (await scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task QueryFilter_WithNoAmbientTenant_ThrowsOnQuery()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext(TenantA);
        scope.Context.Vehicles.Add(CreateVehicle(TenantA));
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        TenantContext.Clear();

        // Assert — fail-closed, but by throwing rather than returning no rows: the filter is
        // "CurrentTenantId.HasValue && TenantId == CurrentTenantId.Value", yet EF funcletizes
        // ".Value" into a query parameter that is evaluated eagerly, before the HasValue guard
        // can short-circuit, so querying a tenant-owned set without an ambient tenant throws.
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.Context.Vehicles.CountAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Nullable object must have a value");
    }

    [Fact]
    public async Task IgnoreQueryFilters_BypassesTenantFilterRegardlessOfAmbientTenant()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext(TenantA);
        scope.Context.Vehicles.Add(CreateVehicle(TenantA));
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert — the escape hatch sees the row both under a foreign tenant and with none.
        TenantContext.SetTenant(TenantB);
        (await scope.Context.Vehicles.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        TenantContext.Clear();
        (await scope.Context.Vehicles.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public void Model_WithDefaultOptions_AddsTenantForeignKeyWithRestrictDeleteAndTenantIdIndex()
    {
        // Arrange & Act — TestDbContext calls ConfigureBlueprintMultiTenancy with the default
        // addTenantForeignKey: true (the pooled single-database topology).
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var entityType = scope.Context.Model.FindEntityType(typeof(TestVehicle)).ShouldNotBeNull();

        // Assert — one FK to the tenant table over TenantId, with Restrict so tenants with data
        // cannot be deleted, plus the TenantId index every filtered query relies on.
        var foreignKey = entityType.GetForeignKeys().ShouldHaveSingleItem();
        foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(TestTenant));
        foreignKey.Properties.ShouldHaveSingleItem().Name.ShouldBe(nameof(IMustHaveTenant.TenantId));
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);

        entityType.GetIndexes().ShouldContain(i => i.Properties.Any(p => p.Name == nameof(IMustHaveTenant.TenantId)));
    }

    [Fact]
    public async Task ConfigureBlueprintMultiTenancy_WithoutTenantForeignKey_StillAppliesFilterAndIndex()
    {
        // Arrange — a split-plane context where tenant-owned business data lives without the
        // tenant table, so no cross-database foreign key may be emitted.
        TenantContext.SetTenant(TenantA);
        var options = new DbContextOptionsBuilder<SplitPlaneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new SplitPlaneDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Vehicles.Add(CreateVehicle(TenantA));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — no foreign key, but the TenantId index survives.
        var entityType = context.Model.FindEntityType(typeof(TestVehicle)).ShouldNotBeNull();
        entityType.GetForeignKeys().ShouldBeEmpty();
        entityType.GetIndexes().ShouldContain(i => i.Properties.Any(p => p.Name == nameof(IMustHaveTenant.TenantId)));

        // The query filter is applied and still tracks the ambient tenant through CurrentTenantId.
        TenantContext.SetTenant(TenantB);
        context.CurrentTenantId.ShouldBe(TenantB);
        (await context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);

        TenantContext.SetTenant(TenantA);
        (await context.Vehicles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public void ConfigureBlueprintMultiTenancy_WithMissingCurrentTenantIdProperty_Throws()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var modelBuilder = new ModelBuilder();

        // Act — point the configuration at a property name the context does not expose.
        var exception = Should.Throw<InvalidOperationException>(
            () => modelBuilder.ConfigureBlueprintMultiTenancy<TestTenant, TestDbContext>(scope.Context, "NoSuchProperty"));

        // Assert — the guard fires during model building, naming the missing property and context.
        exception.Message.ShouldContain("NoSuchProperty");
        exception.Message.ShouldContain(nameof(TestDbContext));
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

    private static TestVehicle CreateVehicle(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "Test Vehicle",
        Kind = TestVehicleKind.Bicycle,
        Capacity = 2,
        PricePerHour = 20m,
    };

    /// <summary>
    /// Minimal context for the <c>addTenantForeignKey: false</c> topology: it holds only the
    /// tenant-owned business entity (no tenant table), so a foreign key would be invalid.
    /// </summary>
    private sealed class SplitPlaneDbContext : DbContext
    {
        /// <summary>
        /// Reads the ambient tenant through an instance member (mirroring BlueprintDbContext) so
        /// EF Core re-evaluates it against the executing context instance per query.
        /// </summary>
        private readonly Func<Guid?> tenantIdAccessor = () => TenantContext.Current;

        public SplitPlaneDbContext(DbContextOptions<SplitPlaneDbContext> options)
            : base(options)
        {
        }

        public Guid? CurrentTenantId => this.tenantIdAccessor();

        public DbSet<TestVehicle> Vehicles => this.Set<TestVehicle>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureBlueprintMultiTenancy<TestTenant, SplitPlaneDbContext>(this, addTenantForeignKey: false);
        }
    }
}
