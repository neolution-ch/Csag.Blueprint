namespace Csag.Blueprint.Infrastructure.UnitTests.Localization;

using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The single-language helpers match language codes case-insensitively, like the current-language
/// ranking does. SQLite compares with a case-sensitive collation by default, so a stored "DE" only
/// matches a requested "de" when the query normalizes both sides.
/// </summary>
public sealed class ExactLanguageMatchingTests
{
    [Fact]
    public void IncludeTextsForLanguage_MatchesRegardlessOfCase()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("p", ("DE", "Stuhl"), ("en", "Chair"));
        db.Save();
        db.Context.ChangeTracker.Clear();

        var product = db.Context.Products.IncludeTextsForLanguage<Product, ProductText>("de").Single();

        product.LocalizedTexts.Select(t => t.Text).ShouldBe(["Stuhl"]);
    }

    [Fact]
    public void IncludeTextsForLanguages_MatchesRegardlessOfCase()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("p", ("DE", "Stuhl"), ("Fr-ch", "Chaise"), ("en", "Chair"));
        db.Save();
        db.Context.ChangeTracker.Clear();

        var product = db.Context.Products.IncludeTextsForLanguages<Product, ProductText>("de", "FR-CH").Single();

        product.LocalizedTexts.Select(t => t.Text).Order(StringComparer.Ordinal).ShouldBe(["Chaise", "Stuhl"]);
    }

    [Fact]
    public void WhereHasTextForLanguage_MatchesRegardlessOfCase()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("with-german", ("DE", "Stuhl"));
        db.AddProduct("without-german", ("en", "Chair"));
        db.Save();

        db.Context.Products.WhereHasTextForLanguage<Product, ProductText>("de")
            .Select(p => p.InternalName)
            .ToList()
            .ShouldBe(["with-german"]);
    }

    [Fact]
    public void GetTextForLanguage_MatchesRegardlessOfCase()
    {
        ICollection<ProductText> texts = [new() { LanguageCode = "DE", Text = "Stuhl" }];

        texts.GetTextForLanguage("de")!.Text.ShouldBe("Stuhl");
    }
}
