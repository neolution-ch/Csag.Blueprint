namespace Csag.Blueprint.Tests.Conventions;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.TestModel;
using Microsoft.EntityFrameworkCore;

public sealed class GuidPrimaryKeyConventionTests
{
    [Fact]
    public void SingleColumnGuidKeys_GetSequentialGuidDefaults()
    {
        using var context = BuildContext();

        DefaultValueSql<Product>(context, nameof(Product.Id)).ShouldBe("NEWSEQUENTIALID()");
        DefaultValueSql<ProductText>(context, nameof(ProductText.Id)).ShouldBe("NEWSEQUENTIALID()");
    }

    [Fact]
    public void NonKeyGuidColumns_AreLeftAlone()
    {
        using var context = BuildContext();

        DefaultValueSql<ProductText>(context, nameof(ProductText.ProductId)).ShouldBeNull();
    }

    private static ConventionContext BuildContext()
        => new(new DbContextOptionsBuilder<ConventionContext>().UseSqlite("Filename=:memory:").Options);

    private static string? DefaultValueSql<TEntity>(DbContext context, string propertyName)
        => context.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetDefaultValueSql();

    private sealed class ConventionContext : DbContext
    {
        public ConventionContext(DbContextOptions<ConventionContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>();
            modelBuilder.ConfigureGuidPrimaryKeyDefaults();
        }
    }
}
