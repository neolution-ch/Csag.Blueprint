namespace Csag.Blueprint.Tests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Minimal context that applies the blueprint model conventions without the identity/tenancy model,
/// so the conventions can be exercised against SQLite.
/// </summary>
public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => this.Set<Product>();

    public DbSet<ProductText> ProductTexts => this.Set<ProductText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureContractConstraints();
        modelBuilder.ConfigureLocalizedTextConventions();
        modelBuilder.ConfigureEntityFiltering();

        // SQLite cannot compare or order DateTimeOffset columns. The binary converter stores a
        // sortable representation so the active-range predicates translate like they do on SQL Server.
        var properties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));

        foreach (var property in properties)
        {
            property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}
