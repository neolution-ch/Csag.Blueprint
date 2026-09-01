namespace Csag.Blueprint.Tests.Filtering;

using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Tests.TestModel;
using Microsoft.EntityFrameworkCore;

public sealed class SoftDeleteQueryFilterTests
{
    [Fact]
    public void GlobalFilter_HidesSoftDeletedRowsByDefault()
    {
        using var db = Seeded();

        Names(db.Context.Products).ShouldBe(["live"]);
    }

    [Fact]
    public void IgnoreSoftDeleteFilter_ReturnsDeletedAndLiveRows()
    {
        using var db = Seeded();

        Names(db.Context.Products.IgnoreSoftDeleteFilter()).ShouldBe(["live", "gone"], ignoreOrder: true);
    }

    [Fact]
    public void OnlySoftDeleted_ReturnsDeletedRowsOnly()
    {
        using var db = Seeded();

        Names(db.Context.Products.OnlySoftDeleted()).ShouldBe(["gone"]);
    }

    [Fact]
    public void SoftDeleteFilter_IsRegisteredUnderItsWellKnownName()
    {
        using var db = new TestDatabase();

        var filters = db.Context.Model.FindEntityType(typeof(Product))!.GetDeclaredQueryFilters();

        filters.Select(f => f.Key).ShouldContain(Infrastructure.Database.BlueprintQueryFilters.SoftDelete);
    }

    [Fact]
    public void IgnoreSoftDeleteFilter_RemovesTheGlobalPredicateFromSql()
    {
        using var db = Seeded();

        db.Context.Products.ToQueryString().ShouldContain("WHERE");
        db.Context.Products.IgnoreSoftDeleteFilter().ToQueryString().ShouldNotContain("WHERE");
    }

    private static TestDatabase Seeded()
    {
        var db = new TestDatabase();
        db.AddProduct("live");
        db.AddProduct("gone").DeletedAt = DateTimeOffset.UtcNow;
        db.Save();
        db.Context.ChangeTracker.Clear();
        return db;
    }

    private static List<string> Names(IQueryable<Product> query)
        => [.. query.Select(p => p.InternalName)];
}
