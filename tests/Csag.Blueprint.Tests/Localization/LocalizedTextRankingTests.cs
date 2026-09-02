namespace Csag.Blueprint.Tests.Localization;

using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Tests.TestModel;
using Microsoft.EntityFrameworkCore;

public sealed class LocalizedTextRankingTests
{
    private const string Current = "de-CH";
    private const string Fallback = "en-GB";

    [Fact]
    public void SelectWithCurrentLanguageText_PrefersExactCurrentLanguage()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de-CH", "Schweizerdeutsch"), ("de-DE", "Hochdeutsch"), ("en-GB", "British"));
        db.Save();

        SelectDescriptions(db).ShouldBe(["Schweizerdeutsch"]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_FallsBackToRegionalVariantOfCurrentLanguage()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de-DE", "Hochdeutsch"), ("en-GB", "British"));
        db.Save();

        SelectDescriptions(db).ShouldBe(["Hochdeutsch"]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_FallsBackToExactFallbackLanguage()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("en-GB", "British"), ("en-US", "American"));
        db.Save();

        SelectDescriptions(db).ShouldBe(["British"]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_FallsBackToRegionalVariantOfFallbackLanguage()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("en-US", "American"));
        db.Save();

        SelectDescriptions(db).ShouldBe(["American"]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_ReturnsNullWhenNoLanguageMatches()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("fr-FR", "Francais"), ("it-IT", "Italiano"));
        db.Save();

        SelectDescriptions(db).ShouldBe([null]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_ReturnsNullWhenEntityHasNoTexts()
    {
        using var db = new TestDatabase();
        db.AddProduct("p");
        db.Save();

        SelectDescriptions(db).ShouldBe([null]);
    }

    [Fact]
    public void SelectWithCurrentLanguageText_ProjectsOtherColumnsAlongsideTheText()
    {
        using var db = new TestDatabase();
        db.AddProduct("boat", ("de-CH", "Pedalo"));
        db.Save();

        var cards = db.Context.Products
            .SelectWithCurrentLanguageText<Product, ProductText, Card>(
                TestDatabase.Language(Current, Fallback),
                (p, description) => new Card(p.InternalName, description))
            .ToList();

        cards.ShouldHaveSingleItem();
        cards[0].InternalName.ShouldBe("boat");
        cards[0].Description.ShouldBe("Pedalo");
    }

    [Fact]
    public void SelectWithCurrentLanguageText_TranslatesToSqlWithoutClientEvaluation()
    {
        using var db = new TestDatabase();

        var sql = db.Context.Products
            .SelectWithCurrentLanguageText<Product, ProductText, Card>(
                TestDatabase.Language(Current, Fallback),
                (p, description) => new Card(p.InternalName, description))
            .ToQueryString();

        // A correlated subquery over the text table proves the ranking ran in the database.
        sql.ShouldContain("ProductTexts");
    }

    [Theory]
    [InlineData("de-CH", "Schweizerdeutsch")]
    [InlineData("de-DE", "Hochdeutsch")]
    [InlineData("en-GB", "British")]
    [InlineData("en-US", "American")]
    [InlineData("fr-FR", null)]
    public void InMemoryRanking_AgreesWithDatabaseRanking(string onlyLanguage, string? expected)
    {
        using var db = new TestDatabase();
        db.AddProduct("p", (onlyLanguage, expected ?? "Unrelated"));
        db.Save();

        var provider = TestDatabase.Language(Current, Fallback);
        var fromDatabase = SelectDescriptions(db).Single();

        db.Context.ChangeTracker.Clear();
        var loaded = db.Context.Products.Include(p => p.LocalizedTexts).Single();
        var inMemory = loaded.LocalizedTexts.GetCurrentLanguageText(provider)?.Text;

        fromDatabase.ShouldBe(expected);
        inMemory.ShouldBe(expected);
    }

    [Fact]
    public void IncludeCurrentLanguageText_LoadsOnlyTheBestMatch()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de-CH", "Schweizerdeutsch"), ("de-DE", "Hochdeutsch"), ("en-GB", "British"));
        db.Save();
        db.Context.ChangeTracker.Clear();

        var product = db.Context.Products
            .IncludeCurrentLanguageText<Product, ProductText>(TestDatabase.Language(Current, Fallback))
            .Single();

        product.LocalizedTexts.ShouldHaveSingleItem();
        product.LocalizedTexts.Single().Text.ShouldBe("Schweizerdeutsch");
    }

    [Fact]
    public void IncludeCurrentLanguageText_LoadsNothingWhenNoLanguageMatches()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("fr-FR", "Francais"));
        db.Save();
        db.Context.ChangeTracker.Clear();

        var product = db.Context.Products
            .IncludeCurrentLanguageText<Product, ProductText>(TestDatabase.Language(Current, Fallback))
            .Single();

        product.LocalizedTexts.ShouldBeEmpty();
    }

    [Fact]
    public void BareLanguageCode_MatchesItsRegionalVariants()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de-DE", "Hochdeutsch"));
        db.Save();

        var descriptions = db.Context.Products
            .SelectWithCurrentLanguageText<Product, ProductText, string?>(
                TestDatabase.Language("de", "en"),
                (p, description) => description)
            .ToList();

        descriptions.ShouldBe(["Hochdeutsch"]);
    }

    [Fact]
    public void RegionalCurrentLanguage_FallsBackToTheBareLanguageCode()
    {
        using var db = new TestDatabase();
        db.AddProduct("p", ("de", "Beschreibung"));
        db.Save();

        var descriptions = db.Context.Products
            .SelectWithCurrentLanguageText<Product, ProductText, string?>(
                TestDatabase.Language("de-CH", "en"),
                (p, description) => description)
            .ToList();

        descriptions.ShouldBe(["Beschreibung"]);
    }

    [Fact]
    public void RegionalCurrentLanguage_FallsBackToTheBareLanguageCode_InMemory()
    {
        var texts = new List<ProductText> { new() { Id = Guid.NewGuid(), LanguageCode = "de", Text = "Beschreibung" } };

        texts.GetCurrentLanguageText(TestDatabase.Language("de-CH", "en"))?.Text.ShouldBe("Beschreibung");
    }

    [Fact]
    public void WhereHasCurrentLanguageText_MatchesBareAndRegionalVariantsOfTheCurrentLanguage()
    {
        using var db = new TestDatabase();
        db.AddProduct("bare", ("de", "Beschreibung"));
        db.AddProduct("regional", ("de-DE", "Hochdeutsch"));
        db.AddProduct("exact", ("de-CH", "Schweizerdeutsch"));
        db.Save();

        var matches = db.Context.Products
            .WhereHasCurrentLanguageText<Product, ProductText>(TestDatabase.Language(Current, Fallback))
            .Select(p => p.InternalName)
            .OrderBy(name => name)
            .ToList();

        matches.ShouldBe(["bare", "exact", "regional"]);
    }

    [Fact]
    public void WhereHasCurrentLanguageText_ExcludesEntitiesThatOnlyHaveFallbackOrUnrelatedText()
    {
        using var db = new TestDatabase();
        db.AddProduct("fallbackOnly", ("en-GB", "British"));
        db.AddProduct("unrelated", ("it-IT", "Italiano"));
        db.Save();

        db.Context.Products
            .WhereHasCurrentLanguageText<Product, ProductText>(TestDatabase.Language(Current, Fallback))
            .ShouldBeEmpty();
    }

    private static List<string?> SelectDescriptions(TestDatabase db)
        => [.. db.Context.Products
            .SelectWithCurrentLanguageText<Product, ProductText, string?>(
                TestDatabase.Language(Current, Fallback),
                (p, description) => description)];

    private sealed record Card(string InternalName, string? Description);
}
