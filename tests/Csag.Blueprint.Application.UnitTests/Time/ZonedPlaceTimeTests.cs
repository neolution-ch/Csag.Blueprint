namespace Csag.Blueprint.Application.UnitTests.Time;

using System.Reflection;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Pins <see cref="ZonedPlaceTime"/>: the reading is kept as written, the zone id must be an IANA id this
/// host resolves, and the instant is derived from both following <see cref="ZonedTime.TryToInstant"/>.
/// </summary>
public sealed class ZonedPlaceTimeTests
{
    [Fact]
    public void Create_ReadingInZone_KeepsReadingAndZoneAndDerivesInstantInUtc()
    {
        var reading = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified);

        var stand = ZonedPlaceTime.Create(reading, "America/New_York");

        stand.Local.ShouldBe(reading);
        stand.TimeZoneId.ShouldBe("America/New_York");
        stand.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero));
        stand.At.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Create_ReadingOfAnyKind_IsKeptAsWrittenWithUnspecifiedKind(DateTimeKind kind)
    {
        var stand = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, kind), "America/New_York");

        stand.Local.ShouldBe(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified));
        stand.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        stand.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryCreate_ReadingInSpringForwardGap_Fails()
    {
        // New York springs forward on 2027-03-14 from 02:00 to 03:00.
        ZonedPlaceTime.TryCreate(new DateTime(2027, 3, 14, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York", out var stand).ShouldBeFalse();

        stand.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ZonedPlaceTime.Create(new DateTime(2027, 3, 14, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York"))
            .ParamName.ShouldBe("local");
    }

    [Fact]
    public void Create_ReadingInFallBackOverlap_DenotesEarlierOccurrence()
    {
        // New York falls back on 2026-11-01 from 02:00 to 01:00, so 01:30 happens at -04:00 and again at -05:00.
        var stand = ZonedPlaceTime.Create(new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified), "America/New_York");

        stand.At.ShouldBe(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("")]
    [InlineData("W. Europe Standard Time")] // a Windows id, which TimeZoneInfo also resolves
    [InlineData("europe/zurich")] // the lookup matches case-insensitively; only the exact spelling is accepted
    [InlineData("Mars/Olympus_Mons")] // well-formed, but no zone
    public void TryCreate_IdThatIsNoResolvableIanaId_Fails(string timeZoneId)
    {
        ZonedPlaceTime.TryCreate(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId, out var stand).ShouldBeFalse();

        stand.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId))
            .ParamName.ShouldBe("timeZoneId");
    }

    [Fact]
    public void TryCreate_WithoutId_Fails()
    {
        ZonedPlaceTime.TryCreate(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), null, out var stand).ShouldBeFalse();

        stand.ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), null!))
            .ParamName.ShouldBe("timeZoneId");
    }

    [Fact]
    public void Equality_SameReadingAndZone_IsEqual()
    {
        var first = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");
        var second = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc), "America/New_York");

        second.ShouldBe(first);
        (first == second).ShouldBeTrue();
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Equality_SameInstantInAnotherZone_IsNotEqual()
    {
        var inNewYork = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");
        var inZurich = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 16, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich");

        inZurich.At.ShouldBe(inNewYork.At, "precondition: both readings denote the same instant");
        inZurich.ShouldNotBe(inNewYork);
    }

    [Fact]
    public void Equality_ViewerTimeWithSameMembers_IsNotEqual()
    {
        var reading = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified);

        ZonedPlaceTime.Create(reading, "Europe/Zurich").Equals(ViewerTime.Create(reading, "Europe/Zurich")).ShouldBeFalse();
    }

    [Fact]
    public void Rederive_UnchangedRules_KeepsValueAndReportsNoShift()
    {
        var stand = ZonedPlaceTime.Create(new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified), "America/New_York");

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
        var stand = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");

        var (moved, shifted) = stand.WithTimeZone("America/Chicago");

        shifted.ShouldBeFalse();
        moved.Local.ShouldBe(stand.Local);
        moved.TimeZoneId.ShouldBe("America/Chicago");
        moved.At.ShouldBe(new DateTimeOffset(2026, 11, 14, 16, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void WithTimeZone_ReadingInGapOfNewZone_MovesItForwardByTheGapAndReportsTheShift()
    {
        var stand = ZonedPlaceTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York");

        var (moved, shifted) = stand.WithTimeZone("Europe/Zurich");

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
        var stand = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York");

        Should.Throw<ArgumentException>(() => stand.WithTimeZone(timeZoneId)).ParamName.ShouldBe("timeZoneId");
        Should.Throw<ArgumentNullException>(() => stand.WithTimeZone(null!)).ParamName.ShouldBe("timeZoneId");
    }

    /// <summary>
    /// Builds a value the way EF Core materializes a stored one, through the private constructor and without
    /// deriving the instant, as rows written under older time zone data are read back.
    /// </summary>
    private static ZonedPlaceTime Stored(DateTime local, string timeZoneId, DateTimeOffset at)
    {
        var constructor = typeof(ZonedPlaceTime).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(DateTime), typeof(string), typeof(DateTimeOffset)])!;
        return (ZonedPlaceTime)constructor.Invoke([local, timeZoneId, at]);
    }
}
