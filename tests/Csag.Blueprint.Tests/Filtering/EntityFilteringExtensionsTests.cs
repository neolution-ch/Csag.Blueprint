namespace Csag.Blueprint.Tests.Filtering;

using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Tests.TestModel;

public sealed class EntityFilteringExtensionsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void WhereActiveNow_KeepsOpenEndedAndCurrentRanges()
    {
        using var db = new TestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveNow())
            .ShouldBe(["always", "current", "started", "ending"], ignoreOrder: true);
    }

    [Fact]
    public void WhereInactiveNow_IsTheComplementOfWhereActiveNow()
    {
        using var db = new TestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereInactiveNow()).ShouldBe(["future", "expired"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveAt_EvaluatesTheCallerChosenInstant()
    {
        using var db = new TestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveAt(Now.AddDays(5)))
            .ShouldBe(["always", "current", "started", "future"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveAt_TreatsActiveUntilAsExclusive()
    {
        using var db = new TestDatabase();
        var boundary = Now.AddDays(3);
        db.AddProduct("bounded").ActiveUntil = boundary;
        db.Save();

        Names(db.Context.Products.WhereActiveAt(boundary.AddSeconds(-1))).ShouldBe(["bounded"]);
        Names(db.Context.Products.WhereActiveAt(boundary)).ShouldBeEmpty();
    }

    [Fact]
    public void WhereActiveInRange_MatchesOverlappingRanges()
    {
        using var db = new TestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveInRange(Now.AddDays(4), Now.AddDays(6)))
            .ShouldBe(["always", "current", "started", "future"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveToday_UsesStartOfUtcDay()
    {
        using var db = new TestDatabase();
        var startOfUtcDay = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        // Active only later today: excluded, because the check is made at midnight UTC.
        db.AddProduct("later-today").ActiveFrom = startOfUtcDay.AddHours(23);
        db.AddProduct("since-midnight").ActiveFrom = startOfUtcDay;
        db.Save();

        Names(db.Context.Products.WhereActiveToday()).ShouldBe(["since-midnight"]);
    }

    [Fact]
    public void WhereDeleted_RequiresOptingOutOfTheGlobalFilterFirst()
    {
        using var db = new TestDatabase();
        db.AddProduct("live");
        db.AddProduct("gone").DeletedAt = Now;
        db.Save();

        Names(db.Context.Products.WhereDeleted()).ShouldBeEmpty();
        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereDeleted()).ShouldBe(["gone"]);
    }

    [Fact]
    public void WhereDeletedBefore_FiltersOnDeletionTimestamp()
    {
        using var db = new TestDatabase();
        db.AddProduct("old").DeletedAt = Now.AddDays(-10);
        db.AddProduct("recent").DeletedAt = Now.AddDays(-1);
        db.Save();

        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereDeletedBefore(Now.AddDays(-5)))
            .ShouldBe(["old"]);
    }

    [Fact]
    public void WhereActiveAndNotDeleted_CombinesBothContracts()
    {
        using var db = new TestDatabase();
        db.AddProduct("live-active");
        var deleted = db.AddProduct("deleted-active");
        deleted.DeletedAt = Now;
        db.AddProduct("live-expired").ActiveUntil = Now.AddDays(-1);
        db.Save();

        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereActiveAndNotDeleted())
            .ShouldBe(["live-active"]);
    }

    private static void SeedActiveRanges(TestDatabase db)
    {
        db.AddProduct("always");

        var current = db.AddProduct("current");
        current.ActiveFrom = Now.AddDays(-1);
        current.ActiveUntil = Now.AddDays(10);

        db.AddProduct("started").ActiveFrom = Now.AddDays(-1);
        db.AddProduct("ending").ActiveUntil = Now.AddDays(1);
        db.AddProduct("future").ActiveFrom = Now.AddDays(1);
        db.AddProduct("expired").ActiveUntil = Now.AddDays(-1);

        db.Save();
    }

    private static List<string> Names(IQueryable<Product> query)
        => [.. query.Select(p => p.InternalName)];
}
