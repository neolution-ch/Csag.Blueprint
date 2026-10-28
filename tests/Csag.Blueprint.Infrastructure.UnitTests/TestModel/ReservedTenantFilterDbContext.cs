namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Context whose application registers its own query filter under the key the blueprint reserves for
/// tenant isolation.
/// </summary>
public sealed class ReservedTenantFilterDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public ReservedTenantFilterDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Booking>().HasQueryFilter(BlueprintQueryFilters.Tenant, b => b.Reference != string.Empty);
    }
}
