namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Context mapping the <see cref="Asset"/> hierarchy table-per-concrete-type.
/// </summary>
public sealed class TpcDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public TpcDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Asset>().UseTpcMappingStrategy();
        builder.Entity<Machine>();
        builder.Entity<Tool>();
    }
}
