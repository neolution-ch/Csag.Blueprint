namespace Csag.Blueprint.Application.UnitTests.Json;

using Csag.Blueprint.Application.Json;

/// <summary>
/// Pins <see cref="LocalDateTimeFormat.Format"/>, which every writer of a local reading goes through: the API's
/// JSON converter and the OpenAPI example converter.
/// </summary>
public sealed class LocalDateTimeFormatTests
{
    [Fact]
    public void Format_UnspecifiedKind_WritesTheReadingWithoutFraction()
    {
        var reading = new DateTime(2026, 10, 26, 10, 0, 30, 500, DateTimeKind.Unspecified);

        LocalDateTimeFormat.Format(reading).ShouldBe("2026-10-26T10:00:30");
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Format_UtcOrLocalKind_ThrowsNamingTheKind(DateTimeKind kind)
    {
        var value = new DateTime(2026, 10, 26, 10, 0, 0, kind);

        var exception = Should.Throw<InvalidOperationException>(() => LocalDateTimeFormat.Format(value));

        exception.Message.ShouldBe(
            $"A DateTime with Kind {kind} is not a local reading. Use DateTimeOffset for an instant, or TimeZones.ToLocalReading(instant, zone) for the reading at a place.");
    }
}
