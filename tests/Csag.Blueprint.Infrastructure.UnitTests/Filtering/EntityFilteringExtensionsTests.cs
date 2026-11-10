namespace Csag.Blueprint.Infrastructure.UnitTests.Filtering;

using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.Extensions.Time.Testing;

public sealed class EntityFilteringExtensionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider clock = new(Now);

    [Fact]
    public void WhereActiveNow_KeepsOpenEndedAndCurrentRanges()
    {
        using var db = new SqliteTestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveNow(this.clock))
            .ShouldBe(["always", "current", "started", "ending"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveNow_ReadsTheClockItIsGiven()
    {
        using var db = new SqliteTestDatabase();
        SeedActiveRanges(db);

        this.clock.Advance(TimeSpan.FromDays(5));

        Names(db.Context.Products.WhereActiveNow(this.clock))
            .ShouldBe(["always", "current", "started", "future"], ignoreOrder: true);
    }

    [Fact]
    public void WhereInactiveNow_IsTheComplementOfWhereActiveNow()
    {
        using var db = new SqliteTestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereInactiveNow(this.clock)).ShouldBe(["future", "expired"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveAt_EvaluatesTheCallerChosenInstant()
    {
        using var db = new SqliteTestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveAt(Now.AddDays(5)))
            .ShouldBe(["always", "current", "started", "future"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveAt_TreatsActiveUntilAsExclusive()
    {
        using var db = new SqliteTestDatabase();
        var boundary = Now.AddDays(3);
        db.AddProduct("bounded").ActiveUntil = boundary;
        db.Save();

        Names(db.Context.Products.WhereActiveAt(boundary.AddSeconds(-1))).ShouldBe(["bounded"]);
        Names(db.Context.Products.WhereActiveAt(boundary)).ShouldBeEmpty();
    }

    [Fact]
    public void WhereActiveInRange_MatchesOverlappingRanges()
    {
        using var db = new SqliteTestDatabase();
        SeedActiveRanges(db);

        Names(db.Context.Products.WhereActiveInRange(Now.AddDays(4), Now.AddDays(6)))
            .ShouldBe(["always", "current", "started", "future"], ignoreOrder: true);
    }

    [Fact]
    public void WhereActiveToday_InUtc_UsesMidnightUtc()
    {
        using var db = new SqliteTestDatabase();
        var midnight = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        // Active only later today: excluded, because the check is made at the start of the day.
        db.AddProduct("later-today").ActiveFrom = midnight.AddHours(11);
        db.AddProduct("since-midnight").ActiveFrom = midnight;
        db.Save();

        Names(db.Context.Products.WhereActiveToday(this.clock, TimeZoneInfo.Utc)).ShouldBe(["since-midnight"]);
    }

    [Fact]
    public void WhereActiveToday_WhenTheZonesDayDiffersFromTheUtcDay_UsesTheStartOfTheZonesDay()
    {
        using var db = new SqliteTestDatabase();

        // 23:30 UTC on 1 July is 01:30 on 2 July in Zurich (UTC+2), whose day started at 22:00 UTC on 1 July.
        this.clock.SetUtcNow(new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.Zero));
        var zurichMidnight = new DateTimeOffset(2026, 7, 1, 22, 0, 0, TimeSpan.Zero);

        db.AddProduct("since-zurich-midnight").ActiveFrom = zurichMidnight;
        db.AddProduct("since-the-utc-evening").ActiveFrom = zurichMidnight.AddHours(-2);
        db.AddProduct("ended-on-the-utc-day").ActiveUntil = zurichMidnight.AddHours(-10);
        db.AddProduct("from-later-in-the-zurich-night").ActiveFrom = zurichMidnight.AddMinutes(30);
        db.Save();

        Names(db.Context.Products.WhereActiveToday(this.clock, FindZone("Europe/Zurich")))
            .ShouldBe(["since-zurich-midnight", "since-the-utc-evening"], ignoreOrder: true);
    }

    [Fact]
    public void WhereDeleted_RequiresOptingOutOfTheGlobalFilterFirst()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("live");
        db.AddProduct("gone").DeletedAt = Now;
        db.Save();

        Names(db.Context.Products.WhereDeleted()).ShouldBeEmpty();
        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereDeleted()).ShouldBe(["gone"]);
    }

    [Fact]
    public void WhereDeletedBefore_FiltersOnDeletionTimestamp()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("old").DeletedAt = Now.AddDays(-10);
        db.AddProduct("recent").DeletedAt = Now.AddDays(-1);
        db.Save();

        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereDeletedBefore(Now.AddDays(-5)))
            .ShouldBe(["old"]);
    }

    [Fact]
    public void WhereActiveAndNotDeleted_CombinesBothContracts()
    {
        using var db = new SqliteTestDatabase();
        db.AddProduct("live-active");
        var deleted = db.AddProduct("deleted-active");
        deleted.DeletedAt = Now;
        db.AddProduct("live-expired").ActiveUntil = Now.AddDays(-1);
        db.Save();

        Names(db.Context.Products.IgnoreSoftDeleteFilter().WhereActiveAndNotDeleted(this.clock))
            .ShouldBe(["live-active"]);
    }

    private static void SeedActiveRanges(SqliteTestDatabase db)
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

    private static TimeZoneInfo FindZone(string id)
    {
        return TimeZones.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }
}
