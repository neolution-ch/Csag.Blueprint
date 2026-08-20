namespace Csag.Blueprint.Tests.Shared.Database;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Concrete closure of <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}"/> for unit tests.
/// The base context applies the Blueprint entity configurations and the multi-tenancy conventions
/// (global tenant query filters, <c>TenantId</c> indexes, and tenant foreign keys) to every
/// <see cref="Csag.Blueprint.Domain.Contracts.IMustHaveTenant"/> entity, including <see cref="TestVehicle"/>.
/// </summary>
public sealed class TestDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the tenant-owned sample vehicles.
    /// </summary>
    /// <remarks>
    /// The DbSet is required so the entity type is discovered by convention before
    /// <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}.OnModelCreating"/>
    /// applies the multi-tenancy query filter to all
    /// <see cref="Csag.Blueprint.Domain.Contracts.IMustHaveTenant"/> entities.
    /// </remarks>
    public DbSet<TestVehicle> Vehicles => this.Set<TestVehicle>();
}
