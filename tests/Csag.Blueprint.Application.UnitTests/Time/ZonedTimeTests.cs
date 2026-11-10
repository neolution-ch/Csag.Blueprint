namespace Csag.Blueprint.Application.UnitTests.Time;

using System.Reflection;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Pins <see cref="ZonedTime"/>: the reading is kept as written, the zone id must be an IANA id this
/// host resolves, and the instant is derived from both following <see cref="TimeZones.TryToInstant"/>.
/// The cases use both of its kinds: a stand at a boat show with its own zone, and an online briefing an
/// organiser sets in theirs.
/// </summary>
public sealed class ZonedTimeTests
{
    [Fact]
    public void Create_ReadingInZone_KeepsReadingAndZoneAndDerivesInstantInUtc()
    {
        var reading = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified);

        var stand = ZonedTime.Create(reading, "America/New_York");

        stand.Local.ShouldBe(reading);
        stand.TimeZoneId.ShouldBe("America/New_York");
        stand.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero));
        stand.At.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Create_OrganisersReading_DerivesTheInstantViewersSee()
    {
        var briefing = ZonedTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), "Europe/Zurich");

        briefing.Local.ShouldBe(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified));
        briefing.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        briefing.TimeZoneId.ShouldBe("Europe/Zurich");
        briefing.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
        TimeZones.ToLocalReading(briefing.At, FindZone("America/New_York")).ShouldBe(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Unspecified));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Create_ReadingOfAnyKind_IsKeptAsWrittenWithUnspecifiedKind(DateTimeKind kind)
    {
        var stand = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, kind), "America/New_York");

        stand.Local.ShouldBe(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified));
        stand.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        stand.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("America/New_York", 14)] // springs forward on 2027-03-14 from 02:00 to 03:00
    [InlineData("Europe/Zurich", 28)] // springs forward on 2027-03-28 from 02:00 to 03:00
    public void TryCreate_ReadingInSpringForwardGap_Fails(string timeZoneId, int day)
    {
        var reading = new DateTime(2027, 3, day, 2, 30, 0, DateTimeKind.Unspecified);

        ZonedTime.TryCreate(reading, timeZoneId, out var value).ShouldBeFalse();

        value.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ZonedTime.Create(reading, timeZoneId)).ParamName.ShouldBe("local");
    }

    [Theory]
    [InlineData("America/New_York", 11, 1, 1, 5)] // falls back on 2026-11-01 from 02:00 to 01:00: 01:30 at -04:00 and at -05:00
    [InlineData("Europe/Zurich", 10, 25, 2, 0)] // falls back on 2026-10-25 from 03:00 to 02:00: 02:30 at +02:00 and at +01:00
    public void Create_ReadingInFallBackOverlap_DenotesEarlierOccurrence(string timeZoneId, int month, int day, int hour, int utcHour)
    {
        var value = ZonedTime.Create(new DateTime(2026, month, day, hour, 30, 0, DateTimeKind.Unspecified), timeZoneId);

        value.At.ShouldBe(new DateTimeOffset(2026, month, day, utcHour, 30, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("")]
    [InlineData("W. Europe Standard Time")] // a Windows id, which TimeZoneInfo also resolves
    [InlineData("europe/zurich")] // the lookup matches case-insensitively; only the exact spelling is accepted
    [InlineData("Mars/Olympus_Mons")] // well-formed, but no zone
    public void TryCreate_IdThatIsNoResolvableIanaId_Fails(string timeZoneId)
    {
        ZonedTime.TryCreate(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId, out var stand).ShouldBeFalse();

        stand.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId))
            .ParamName.ShouldBe("timeZoneId");
    }

    [Fact]
    public void TryCreate_WithoutId_Fails()
    {
        ZonedTime.TryCreate(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), null, out var stand).ShouldBeFalse();

        stand.ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), null!))
            .ParamName.ShouldBe("timeZoneId");
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Equality_SameReadingAndZone_IsEqual(DateTimeKind kind)
    {
        var first = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");
        var second = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, kind), "America/New_York");

        second.ShouldBe(first);
        (first == second).ShouldBeTrue();
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Equality_SameInstantInAnotherZone_IsNotEqual()
    {
        var inNewYork = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");
        var inZurich = ZonedTime.Create(new DateTime(2026, 11, 14, 16, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich");
        var inBerlin = ZonedTime.Create(inZurich.Local, "Europe/Berlin");

        inZurich.At.ShouldBe(inNewYork.At, "precondition: both readings denote the same instant");
        inZurich.ShouldNotBe(inNewYork);
        inBerlin.At.ShouldBe(inZurich.At, "precondition: Berlin and Zurich share their rules");
        inBerlin.ShouldNotBe(inZurich, "the same reading in another zone is another organiser's reading");
    }

    [Fact]
    public void Rederive_UnchangedRules_KeepsValueAndReportsNoShift()
    {
        var briefing = ZonedTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich");

        briefing.Rederive().ShouldBe((briefing, false));
    }

    [Fact]
    public void Rederive_ReadingInFallBackOverlapUnderUnchangedRules_KeepsTheEarlierOccurrence()
    {
        var stand = ZonedTime.Create(new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified), "America/New_York");

        var (rederived, shifted) = stand.Rederive();

        rederived.ShouldBe(stand);
        shifted.ShouldBeFalse();
    }

    [Fact]
    public void Rederive_StoredInstantOutOfDate_DerivesItAgainFromTheReading()
    {
        // As stored before a time zone data update that moved Zurich's offset: the reading is kept.
        var stored = Stored(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich", new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        var (rederived, shifted) = stored.Rederive();

        shifted.ShouldBeFalse();
        rederived.Local.ShouldBe(stored.Local);
        rederived.TimeZoneId.ShouldBe("Europe/Zurich");
        rederived.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rederive_ReadingNowInGap_MovesItForwardByTheGapAndReportsTheShift()
    {
        // As stored before a time zone data update that introduced the spring-forward gap 02:30 now falls in.
        var stored = Stored(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "Europe/Zurich", new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));

        var (rederived, shifted) = stored.Rederive();

        shifted.ShouldBeTrue();
        rederived.Local.ShouldBe(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified));
        rederived.TimeZoneId.ShouldBe("Europe/Zurich");
        rederived.At.ShouldBe(new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rederive_StoredIdThisHostCannotResolve_Throws()
    {
        var stored = Stored(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), "Mars/Olympus_Mons", new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));

        Should.Throw<TimeZoneNotFoundException>(() => stored.Rederive()).Message.ShouldContain("Mars/Olympus_Mons");
    }

    [Fact]
    public void WithTimeZone_AnotherZone_KeepsReadingAndDerivesItsInstantThere()
    {
        var stand = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");

        var (moved, shifted) = stand.WithTimeZone("America/Chicago");

        shifted.ShouldBeFalse();
        moved.Local.ShouldBe(stand.Local);
        moved.TimeZoneId.ShouldBe("America/Chicago");
        moved.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 16, 0, 0, TimeSpan.Zero));
        stand.WithTimeZone("Asia/Tokyo").ShouldBe((ZonedTime.Create(stand.Local, "Asia/Tokyo"), false));
    }

    [Fact]
    public void WithTimeZone_ReadingInGapOfNewZone_MovesItForwardByTheGapAndReportsTheShift()
    {
        var briefing = ZonedTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York");

        var (moved, shifted) = briefing.WithTimeZone("Europe/Zurich");

        shifted.ShouldBeTrue();
        moved.Local.ShouldBe(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified));
        moved.TimeZoneId.ShouldBe("Europe/Zurich");
        moved.At.ShouldBe(new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    public void WithTimeZone_IdThatIsNoResolvableIanaId_Throws(string timeZoneId)
    {
        var stand = ZonedTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");

        Should.Throw<ArgumentException>(() => stand.WithTimeZone(timeZoneId)).ParamName.ShouldBe("timeZoneId");
        Should.Throw<ArgumentNullException>(() => stand.WithTimeZone(null!)).ParamName.ShouldBe("timeZoneId");
    }

    /// <summary>
    /// Builds a value the way EF Core materializes a stored one, through the private constructor and without
    /// deriving the instant, as rows written under older time zone data are read back.
    /// </summary>
    private static ZonedTime Stored(DateTime local, string timeZoneId, DateTimeOffset at)
    {
        var constructor = typeof(ZonedTime).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(DateTime), typeof(string), typeof(DateTimeOffset)])!;
        return (ZonedTime)constructor.Invoke([local, timeZoneId, at]);
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return TimeZones.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }
}
