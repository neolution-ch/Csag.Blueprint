namespace Csag.Blueprint.Application.UnitTests.Time;

using System.Reflection;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Pins <see cref="ViewerTime"/>: the organiser's reading is kept as written, the zone id must be an IANA id
/// this host resolves, and the instant viewers see is derived from both following
/// <see cref="ZonedTime.TryToInstant"/>.
/// </summary>
public sealed class ViewerTimeTests
{
    [Fact]
    public void Create_OrganisersReading_DerivesTheInstantViewersSee()
    {
        var briefing = ViewerTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), "Europe/Zurich");

        briefing.Local.ShouldBe(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified));
        briefing.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        briefing.TimeZoneId.ShouldBe("Europe/Zurich");
        briefing.At.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
        ZonedTime.ToLocalReading(briefing.At, FindZone("America/New_York")).ShouldBe(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void TryCreate_ReadingInSpringForwardGap_Fails()
    {
        ViewerTime.TryCreate(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "Europe/Zurich", out var briefing).ShouldBeFalse();

        briefing.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ViewerTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "Europe/Zurich"))
            .ParamName.ShouldBe("local");
    }

    [Fact]
    public void Create_ReadingInFallBackOverlap_DenotesEarlierOccurrence()
    {
        var briefing = ViewerTime.Create(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), "Europe/Zurich");

        briefing.At.ShouldBe(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    public void TryCreate_IdThatIsNoResolvableIanaId_Fails(string? timeZoneId)
    {
        ViewerTime.TryCreate(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId, out var briefing).ShouldBeFalse();

        briefing.ShouldBeNull();
        Should.Throw<ArgumentException>(() => ViewerTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), timeZoneId!))
            .ParamName.ShouldBe("timeZoneId");
    }

    [Fact]
    public void Equality_SameReadingAndZone_IsEqual()
    {
        var first = ViewerTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich");
        var second = ViewerTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Local), "Europe/Zurich");

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
        ViewerTime.Create(first.Local, "Europe/Berlin").ShouldNotBe(first, "the same instant in another zone is another organiser's reading");
    }

    [Fact]
    public void Rederive_ReadingNowInGap_MovesItForwardByTheGapAndReportsTheShift()
    {
        var stored = Stored(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "Europe/Zurich", new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));

        var (rederived, shifted) = stored.Rederive();

        shifted.ShouldBeTrue();
        rederived.Local.ShouldBe(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified));
        rederived.At.ShouldBe(new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rederive_UnchangedRules_KeepsValueAndReportsNoShift()
    {
        var briefing = ViewerTime.Create(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified), "Europe/Zurich");

        briefing.Rederive().ShouldBe((briefing, false));
    }

    [Fact]
    public void WithTimeZone_OrganiserInAnotherZone_KeepsReadingAndDerivesItsInstantThere()
    {
        var briefing = ViewerTime.Create(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "America/New_York");

        briefing.WithTimeZone("Asia/Tokyo").ShouldBe((ViewerTime.Create(briefing.Local, "Asia/Tokyo"), false));

        var (moved, shifted) = briefing.WithTimeZone("Europe/Zurich");
        shifted.ShouldBeTrue();
        moved.Local.ShouldBe(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified));
        Should.Throw<ArgumentException>(() => briefing.WithTimeZone("Mars/Olympus_Mons")).ParamName.ShouldBe("timeZoneId");
    }

    /// <summary>
    /// Builds a value the way EF Core materializes a stored one, through the private constructor and without
    /// deriving the instant, as rows written under older time zone data are read back.
    /// </summary>
    private static ViewerTime Stored(DateTime local, string timeZoneId, DateTimeOffset at)
    {
        var constructor = typeof(ViewerTime).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(DateTime), typeof(string), typeof(DateTimeOffset)])!;
        return (ViewerTime)constructor.Invoke([local, timeZoneId, at]);
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }
}
