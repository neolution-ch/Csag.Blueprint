namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Context mapping a TPH hierarchy whose root adopts the soft-delete contract.
/// </summary>
public sealed class InheritanceDbContext : DbContext
{
    public InheritanceDbContext(DbContextOptions<InheritanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => this.Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Document>();
        modelBuilder.Entity<Invoice>();
        modelBuilder.Entity<Receipt>();

        modelBuilder.ConfigureEntityFiltering();
    }
}
