namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Application-shaped context mapping owned types, some of them implementing blueprint contracts,
/// registered after the base call.
/// </summary>
public sealed class OwnedTypeDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public OwnedTypeDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Shipment>(shipment =>
        {
            shipment.OwnsOne(s => s.Address);
            shipment.OwnsMany(s => s.Lines);
        });

        builder.Entity<Brochure>().OwnsMany(b => b.LocalizedTexts);
    }
}
