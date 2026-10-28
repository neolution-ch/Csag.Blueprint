namespace Csag.Blueprint.Application.UnitTests.Time;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// Pins <see cref="ZonedTime"/> against the daylight saving transitions of real zones. Every zone is
/// named explicitly and every clock is fixed, so the results do not depend on the machine's own zone.
/// The transition dates come from the IANA rules in force for 2026 and 2027; a precondition in each
/// test names the assumption, so a tzdata change reads as such rather than as a logic failure.
/// </summary>
public sealed class ZonedTimeTests
{
    private static readonly TimeZoneInfo Zurich = FindZone("Europe/Zurich");
    private static readonly TimeZoneInfo NewYork = FindZone("America/New_York");
    private static readonly TimeZoneInfo Santiago = FindZone("America/Santiago");
    private static readonly TimeZoneInfo Havana = FindZone("America/Havana");

    [Fact]
    public void TryToInstant_ReadingInSpringForwardGap_DoesNotExist()
    {
        // Zurich springs forward on 2027-03-28 from 02:00 to 03:00, so 02:30 is never shown on a clock.
        ZonedTime.TryToInstant(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), Zurich, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, 59, "2027-03-28T00:59:00Z")]
    [InlineData(3, 0, "2027-03-28T01:00:00Z")]
    public void TryToInstant_ReadingsAroundSpringForwardGap_Exist(int hour, int minute, string expectedUtc)
    {
        ZonedTime.TryToInstant(new DateTime(2027, 3, 28, hour, minute, 0, DateTimeKind.Unspecified), Zurich, out var instant).ShouldBeTrue();

        instant.ShouldBe(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryToInstant_ReadingInFallBackOverlap_ResolvesToEarlierOccurrence()
    {
        // Zurich falls back on 2026-10-25 from 03:00 to 02:00, so 02:30 happens at +02:00 and again at +01:00.
        ZonedTime.TryToInstant(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), Zurich, out var instant).ShouldBeTrue();

        instant.ShouldBe(new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2)));
        Zurich.GetUtcOffset(instant).ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void TryToInstant_ReturnsInstantInUtc()
    {
        ZonedTime.TryToInstant(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), Zurich, out var instant).ShouldBeTrue();

        instant.Offset.ShouldBe(TimeSpan.Zero);
        instant.UtcDateTime.ShouldBe(new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void TryToInstant_IgnoresKindOfReading(DateTimeKind kind)
    {
        var reading = new DateTime(2026, 7, 1, 10, 0, 0, kind);

        ZonedTime.TryToInstant(reading, Zurich, out var instant).ShouldBeTrue();

        instant.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToLocalReading_ReturnsClockReadingOfZoneWithUnspecifiedKind()
    {
        var reading = ZonedTime.ToLocalReading(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), Zurich);

        // 01:30Z is half an hour after the fall-back transition, the second time the clock shows 02:30.
        reading.ShouldBe(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified));
        reading.Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public void DayRange_SpringForwardDay_Spans23Hours()
    {
        var (start, end) = ZonedTime.DayRange(new DateOnly(2027, 3, 28), Zurich);

        start.ShouldBe(new DateTimeOffset(2027, 3, 27, 23, 0, 0, TimeSpan.Zero));
        end.ShouldBe(new DateTimeOffset(2027, 3, 28, 22, 0, 0, TimeSpan.Zero));
        (end - start).ShouldBe(TimeSpan.FromHours(23));
    }

    [Fact]
    public void DayRange_FallBackDay_Spans25Hours()
    {
        var (start, end) = ZonedTime.DayRange(new DateOnly(2026, 10, 25), Zurich);

        start.ShouldBe(new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero));
        end.ShouldBe(new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero));
        (end - start).ShouldBe(TimeSpan.FromHours(25));
    }

    [Fact]
    public void DayRange_OrdinaryDay_Spans24HoursFromLocalMidnight()
    {
        var (start, end) = ZonedTime.DayRange(new DateOnly(2026, 10, 21), Zurich);

        start.ShouldBe(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(2)));
        end.ShouldBe(new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.FromHours(2)));
        start.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void DayRange_DayWhoseMidnightDoesNotExist_StartsAtTransition()
    {
        // Chile springs forward at midnight: on 2026-09-06 clocks jump from 00:00 (-04:00) to 01:00 (-03:00).
        var date = new DateOnly(2026, 9, 6);
        Santiago.IsInvalidTime(date.ToDateTime(TimeOnly.MinValue)).ShouldBeTrue("precondition: midnight of 2026-09-06 does not exist in America/Santiago");

        var (start, end) = ZonedTime.DayRange(date, Santiago);

        start.ShouldBe(new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero));
        ZonedTime.ToLocalReading(start, Santiago).ShouldBe(new DateTime(2026, 9, 6, 1, 0, 0, DateTimeKind.Unspecified));
        ZonedTime.ToLocalReading(start.AddSeconds(-1), Santiago).Date.ShouldBe(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Unspecified));
        end.ShouldBe(new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.FromHours(-3)));
        (end - start).ShouldBe(TimeSpan.FromHours(23));
    }

    [Fact]
    public void DayRange_DayBeforeMissingMidnight_EndsWhereNextDayStarts()
    {
        var (_, end) = ZonedTime.DayRange(new DateOnly(2026, 9, 5), Santiago);
        var (nextStart, _) = ZonedTime.DayRange(new DateOnly(2026, 9, 6), Santiago);

        end.ShouldBe(nextStart);
    }

    [Fact]
    public void DayRange_DayWhoseMidnightHappensTwice_StartsAtEarlierMidnight()
    {
        // Cuba falls back at midnight: on 2026-11-01 clocks go from 01:00 (-04:00) back to 00:00 (-05:00).
        var date = new DateOnly(2026, 11, 1);
        Havana.IsAmbiguousTime(date.ToDateTime(TimeOnly.MinValue)).ShouldBeTrue("precondition: midnight of 2026-11-01 happens twice in America/Havana");

        var (start, end) = ZonedTime.DayRange(date, Havana);

        start.ShouldBe(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4)));
        (end - start).ShouldBe(TimeSpan.FromHours(25));
    }

    [Fact]
    public void DayRange_LastRepresentableDate_EndsAtMaxValue()
    {
        var (_, end) = ZonedTime.DayRange(DateOnly.MaxValue, Zurich);

        end.ShouldBe(DateTimeOffset.MaxValue);
    }

    [Theory]
    [InlineData("2026-10-21T21:59:00Z", "2026-10-21")] // 23:59 in Zurich
    [InlineData("2026-10-21T22:00:00Z", "2026-10-22")] // midnight in Zurich, still the 21st in UTC
    public void Today_ReturnsDateOfZoneNotOfUtc(string utcNow, string expectedDate)
    {
        var clock = new FixedTimeProvider(DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture));

        ZonedTime.Today(clock, Zurich).ShouldBe(DateOnly.Parse(expectedDate, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Today_SameInstant_DiffersBetweenZones()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 21, 22, 30, 0, TimeSpan.Zero));

        ZonedTime.Today(clock, Zurich).ShouldBe(new DateOnly(2026, 10, 22));
        ZonedTime.Today(clock, TimeZoneInfo.Utc).ShouldBe(new DateOnly(2026, 10, 21));
        ZonedTime.Today(clock, NewYork).ShouldBe(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void TryToInstant_ReadingInPastGapOfZoneThatDroppedDaylightSavingTime_DoesNotExist()
    {
        // Chihuahua sprang forward from 02:00 to 03:00 on 2022-04-03 and dropped daylight saving time that
        // October. Linux's TimeZoneInfo.IsInvalidTime misses the gap; converting back does not.
        ZonedTime.TryToInstant(new DateTime(2022, 4, 3, 2, 30, 0, DateTimeKind.Unspecified), FindZone("America/Chihuahua"), out _).ShouldBeFalse();
    }

    [Fact]
    public void DayRange_DayWhoseMidnightFellInPastGapOfZoneThatDroppedDaylightSavingTime_StartsAtTransition()
    {
        // Paraguay sprang forward at midnight on 2024-10-06 (from -04:00 to -03:00) and kept -03:00 from that
        // month on, so the day began at 01:00 local, 04:00Z.
        var asuncion = FindZone("America/Asuncion");

        var (start, _) = ZonedTime.DayRange(new DateOnly(2024, 10, 6), asuncion);

        start.ShouldBe(new DateTimeOffset(2024, 10, 6, 4, 0, 0, TimeSpan.Zero));
        ZonedTime.ToLocalReading(start, asuncion).ShouldBe(new DateTime(2024, 10, 6, 1, 0, 0, DateTimeKind.Unspecified));
    }

    [Theory]
    [InlineData("Europe/Zurich")]
    [InlineData("America/New_York")]
    [InlineData("UTC")] // what a browser on a machine set to UTC reports
    [InlineData("Etc/UTC")]
    [InlineData("Etc/GMT+5")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("America/Argentina/Buenos_Aires")]
    public void TryFindIanaTimeZone_IanaId_FindsZoneWithThatId(string id)
    {
        ZonedTime.TryFindIanaTimeZone(id, out var zone).ShouldBeTrue();

        zone.ShouldNotBeNull();
        zone.Id.ShouldBe(id);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Eastern Standard Time")]
    [InlineData("Pacific SA Standard Time")]
    public void TryFindIanaTimeZone_WindowsId_IsRejected(string id)
    {
        ZonedTime.TryFindIanaTimeZone(id, out var zone).ShouldBeFalse();

        zone.ShouldBeNull();
    }

    [Theory]
    [InlineData("Europe//Zurich")] // the remaining rejects resolve on Linux, which reads the id as a path
    [InlineData("America//New_York")]
    [InlineData("localtime")]
    [InlineData("posixrules")]
    [InlineData("Factory")]
    [InlineData("right/Europe/Zurich")]
    [InlineData("right/UTC")]
    [InlineData("posix/Europe/Zurich")]
    [InlineData("/Europe/Zurich")]
    [InlineData("Europe/Zurich/")]
    [InlineData("Europe/./Zurich")]
    [InlineData("../zoneinfo/Europe/Zurich")]
    public void TryFindIanaTimeZone_PathThatIsNoIanaId_IsRejected(string id)
    {
        ZonedTime.IsWellFormedIanaTimeZoneId(id).ShouldBeFalse();
        ZonedTime.TryFindIanaTimeZone(id, out var zone).ShouldBeFalse();
        zone.ShouldBeNull();
    }

    [Theory]
    [InlineData("Europe/Zurich")]
    [InlineData("Etc/GMT-14")]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("EST5EDT")] // legacy names, which Ubuntu ships separately in tzdata-legacy
    [InlineData("US/Eastern")]
    [InlineData("Mars/Olympus_Mons")] // well-formed, though no tz database knows it
    public void IsWellFormedIanaTimeZoneId_NameOfTheIanaForm_IsTrueWhetherKnownOrNot(string id)
    {
        ZonedTime.IsWellFormedIanaTimeZoneId(id).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe/Zürich")]
    [InlineData("+02:00")]
    public void IsWellFormedIanaTimeZoneId_ValueOfAnotherForm_IsFalse(string? id)
    {
        ZonedTime.IsWellFormedIanaTimeZoneId(id).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Europe/Atlantis")]
    [InlineData("europe/zurich")] // the lookup matches case-insensitively; only the exact spelling is accepted
    [InlineData("+02:00")]
    public void TryFindIanaTimeZone_UnknownOrMiscasedId_IsRejected(string? id)
    {
        ZonedTime.TryFindIanaTimeZone(id, out var zone).ShouldBeFalse();

        zone.ShouldBeNull();
    }

    [Theory]
    [InlineData(24, 2, -4)] // both on summer time
    [InlineData(25, 1, -4)] // Europe has fallen back, the United States has not: five hours apart
    [InlineData(28, 1, -4)]
    [InlineData(31, 1, -4)]
    [InlineData(32, 1, -5)] // 1 November: the United States falls back too
    public void Offsets_NewYorkAndZurich_ArePartlyOutOfStepAroundTheirTransitions(int dayOfOctober, int zurichHours, int newYorkHours)
    {
        var noon = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Unspecified).AddDays(dayOfOctober - 1);

        ZonedTime.TryToInstant(noon, Zurich, out var zurichNoon).ShouldBeTrue();
        ZonedTime.TryToInstant(noon, NewYork, out var newYorkNoon).ShouldBeTrue();

        Zurich.GetUtcOffset(zurichNoon).ShouldBe(TimeSpan.FromHours(zurichHours));
        NewYork.GetUtcOffset(newYorkNoon).ShouldBe(TimeSpan.FromHours(newYorkHours));
    }

    [Fact]
    public void ToLocalReading_MeetingDuringOutOfStepWeek_IsFiveHoursEarlierInNewYork()
    {
        ZonedTime.TryToInstant(new DateTime(2026, 10, 28, 15, 0, 0, DateTimeKind.Unspecified), Zurich, out var meeting).ShouldBeTrue();

        ZonedTime.ToLocalReading(meeting, NewYork).ShouldBe(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Unspecified));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
