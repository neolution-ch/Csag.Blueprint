namespace Csag.Blueprint.Application.UnitTests.Time;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// Pins <see cref="PlaceTime"/>: the reading is kept as written and the instant derived from it with the
/// zone the caller passes, following <see cref="ZonedTime.TryToInstant"/>. Every zone is named explicitly,
/// so the results do not depend on the machine's own zone.
/// </summary>
public sealed class PlaceTimeTests
{
    private static readonly TimeZoneInfo Zurich = FindZone("Europe/Zurich");
    private static readonly TimeZoneInfo NewYork = FindZone("America/New_York");

    [Fact]
    public void Create_ReadingInZone_KeepsReadingAndDerivesInstantInUtc()
    {
        var reading = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var placeTime = PlaceTime.Create(reading, Zurich);

        placeTime.Local.ShouldBe(reading);
        placeTime.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
        placeTime.At.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Create_ReadingOfAnyKind_IsKeptAsWrittenWithUnspecifiedKind(DateTimeKind kind)
    {
        var placeTime = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, kind), Zurich);

        placeTime.Local.ShouldBe(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified));
        placeTime.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        placeTime.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryCreate_ReadingInSpringForwardGap_Fails()
    {
        // Zurich springs forward on 2027-03-28 from 02:00 to 03:00, so 02:30 is never shown on a clock.
        PlaceTime.TryCreate(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), Zurich, out var placeTime).ShouldBeFalse();

        placeTime.ShouldBeNull();
    }

    [Fact]
    public void Create_ReadingInSpringForwardGap_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() => PlaceTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), Zurich));

        exception.ParamName.ShouldBe("local");
        exception.Message.ShouldContain("2027-03-28T02:30:00 does not exist in Europe/Zurich");
    }

    [Fact]
    public void Create_ReadingInFallBackOverlap_DenotesEarlierOccurrence()
    {
        // Zurich falls back on 2026-10-25 from 03:00 to 02:00, so 02:30 happens at +02:00 and again at +01:00.
        PlaceTime.TryCreate(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), Zurich, out var placeTime).ShouldBeTrue();

        placeTime.At.ShouldBe(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Create_WithoutZone_Throws()
    {
        Should.Throw<ArgumentNullException>(() => PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), null!));
        Should.Throw<ArgumentNullException>(() => PlaceTime.TryCreate(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), null!, out _));
    }

    [Fact]
    public void Equality_SameReadingInSameZone_IsEqual()
    {
        var first = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), Zurich);
        var second = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), Zurich);

        second.ShouldBe(first);
        (first == second).ShouldBeTrue();
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Equality_SameReadingInAnotherZone_IsNotEqual()
    {
        var inZurich = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), Zurich);
        var inNewYork = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), NewYork);

        inNewYork.ShouldNotBe(inZurich);
        (inZurich != inNewYork).ShouldBeTrue();
    }

    [Fact]
    public void Rederive_UnchangedRules_KeepsValueAndReportsNoShift()
    {
        var placeTime = PlaceTime.Create(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), Zurich);

        var (rederived, shifted) = placeTime.Rederive(Zurich);

        rederived.ShouldBe(placeTime);
        shifted.ShouldBeFalse();
    }

    [Fact]
    public void Rederive_AnotherZone_KeepsReadingAndDerivesItsInstantThere()
    {
        var placeTime = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), Zurich);

        var (rederived, shifted) = placeTime.Rederive(NewYork);

        rederived.Local.ShouldBe(placeTime.Local);
        rederived.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero));
        shifted.ShouldBeFalse();
    }

    [Fact]
    public void Rederive_ReadingNowInGap_MovesItForwardByTheGapAndReportsTheShift()
    {
        // New York already sprang forward on 2027-03-14, so 02:30 on 2027-03-28 exists there but not in Zurich.
        var placeTime = PlaceTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), NewYork);

        var (rederived, shifted) = placeTime.Rederive(Zurich);

        shifted.ShouldBeTrue();
        rederived.Local.ShouldBe(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified));
        rederived.At.ShouldBe(new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rederive_ReadingInHalfHourGap_MovesItForwardByHalfAnHour()
    {
        // Lord Howe Island springs forward by half an hour: on 2026-10-04 its clocks jump from 02:00 (+10:30)
        // to 02:30 (+11:00).
        var lordHowe = FindZone("Australia/Lord_Howe");
        var reading = new DateTime(2026, 10, 4, 2, 15, 0, DateTimeKind.Unspecified);
        lordHowe.IsInvalidTime(reading).ShouldBeTrue("precondition: 02:15 on 2026-10-04 does not exist on Lord Howe Island");
        var placeTime = PlaceTime.Create(reading, Zurich);

        var (rederived, shifted) = placeTime.Rederive(lordHowe);

        shifted.ShouldBeTrue();
        rederived.Local.ShouldBe(new DateTime(2026, 10, 4, 2, 45, 0, DateTimeKind.Unspecified));
        rederived.At.ShouldBe(new DateTimeOffset(2026, 10, 3, 15, 45, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rederive_WithoutZone_Throws()
    {
        var placeTime = PlaceTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), Zurich);

        Should.Throw<ArgumentNullException>(() => placeTime.Rederive(null!));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }
}
