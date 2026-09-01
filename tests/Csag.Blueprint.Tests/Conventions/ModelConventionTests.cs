namespace Csag.Blueprint.Tests.Conventions;

using Csag.Blueprint.Tests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public sealed class ModelConventionTests
{
    [Fact]
    public void InternalName_IsRequiredAndLengthConstrained()
    {
        using var db = new TestDatabase();
        var property = Property<Product>(db, nameof(Product.InternalName));

        property.IsNullable.ShouldBeFalse();
        property.GetMaxLength().ShouldBe(200);
    }

    [Fact]
    public void LocalizedTextColumns_AreRequiredAndLengthConstrained()
    {
        using var db = new TestDatabase();

        Property<ProductText>(db, nameof(ProductText.Text)).GetMaxLength().ShouldBe(2000);
        Property<ProductText>(db, nameof(ProductText.LanguageCode)).GetMaxLength().ShouldBe(10);
        Property<ProductText>(db, nameof(ProductText.Text)).IsNullable.ShouldBeFalse();
        Property<ProductText>(db, nameof(ProductText.LanguageCode)).IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void LocalizedTexts_AreUniquePerLanguagePerOwner()
    {
        using var db = new TestDatabase();

        var index = Indexes<ProductText>(db).Single(i => i.IsUnique);
        index.Properties.Select(p => p.Name).ShouldBe([nameof(ProductText.ProductId), nameof(ProductText.LanguageCode)]);
    }

    [Fact]
    public void DuplicateLanguageForTheSameOwner_IsRejected()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de-CH", "one"), ("de-CH", "two"));

        Should.Throw<DbUpdateException>(db.Save);
    }

    [Fact]
    public void DeletingAnOwner_CascadesToItsTexts()
    {
        using var db = new TestDatabase();
        var product = db.AddProduct("p", ("de-CH", "text"));
        db.Save();

        db.Context.Products.Remove(product);
        db.Save();

        db.Context.ProductTexts.ShouldBeEmpty();
    }

    [Fact]
    public void SoftDeleteColumn_IsIndexed()
    {
        using var db = new TestDatabase();

        Indexes<Product>(db)
            .ShouldContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Product.DeletedAt));
    }

    [Fact]
    public void ActiveRange_HasOneCompositeIndexAndNoRedundantSingleColumnIndexes()
    {
        using var db = new TestDatabase();
        var indexes = Indexes<Product>(db).ToList();

        indexes.ShouldContain(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Product.ActiveFrom), nameof(Product.ActiveUntil) }));

        indexes.ShouldNotContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Product.ActiveFrom));
        indexes.ShouldNotContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Product.ActiveUntil));
    }

    [Fact]
    public void LanguageCode_IsIndexedExactlyOnce()
    {
        using var db = new TestDatabase();

        Indexes<ProductText>(db)
            .Count(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(ProductText.LanguageCode))
            .ShouldBe(1);
    }

    private static IProperty Property<TEntity>(TestDatabase db, string name)
        => db.Context.Model.FindEntityType(typeof(TEntity))!.FindProperty(name)!;

    private static IEnumerable<IIndex> Indexes<TEntity>(TestDatabase db)
        => db.Context.Model.FindEntityType(typeof(TEntity))!.GetIndexes();
}
