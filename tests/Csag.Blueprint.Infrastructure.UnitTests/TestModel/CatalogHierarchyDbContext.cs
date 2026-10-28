namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>Maps the catalog TPH hierarchy and applies the contract-driven conventions to it.</summary>
public sealed class CatalogHierarchyDbContext : DbContext
{
    public CatalogHierarchyDbContext(DbContextOptions<CatalogHierarchyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Catalog> Catalogs => this.Set<Catalog>();

    public DbSet<CatalogText> CatalogTexts => this.Set<CatalogText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Catalog>();
        modelBuilder.Entity<BookCatalog>();
        modelBuilder.Entity<FilmCatalog>();

        modelBuilder.ConfigureContractConstraints();
        modelBuilder.ConfigureLocalizedTextConventions();
    }
}
