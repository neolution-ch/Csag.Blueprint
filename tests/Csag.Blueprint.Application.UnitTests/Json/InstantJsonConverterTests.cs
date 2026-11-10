namespace Csag.Blueprint.Application.UnitTests.Json;

using System.Text.Json;
using Csag.Blueprint.Application.Json;

/// <summary>
/// Pins the wire format of <see cref="DateTimeOffset"/> under <see cref="BlueprintJsonOptions.Default"/>: an
/// instant is read only with a UTC offset or <c>Z</c>, never in the server's zone, and written as
/// System.Text.Json writes it. Uses the production options so the tests fail if the converter is ever
/// unregistered.
/// </summary>
public sealed class InstantJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = BlueprintJsonOptions.Default;

    private static readonly DateTimeOffset EightUtc = new(2026, 7, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-07-01T08:00:00Z", 0)]
    [InlineData("2026-07-01T10:00:00+02:00", 2)]
    [InlineData("2026-07-01T03:00:00-05:00", -5)]
    [InlineData("2026-07-01T10:00+02:00", 2)]
    [InlineData("2026-07-01T10:00:00.0000000+02:00", 2)]
    public void Deserialize_ValueWithOffsetOrZ_ReadsTheInstantWithItsOffset(string text, int offsetHours)
    {
        var value = JsonSerializer.Deserialize<Instants>($$"""{"at":"{{text}}"}""", Options);

        value.ShouldNotBeNull();
        value.At.ShouldBe(EightUtc);
        value.At.Offset.ShouldBe(TimeSpan.FromHours(offsetHours));
    }

    [Fact]
    public void Deserialize_FractionalSeconds_AreKept()
    {
        var value = JsonSerializer.Deserialize<Instants>("""{"at":"2026-07-01T08:00:00.1234567Z"}""", Options);

        value.ShouldNotBeNull();
        value.At.ShouldBe(EightUtc.AddTicks(1_234_567));
    }

    [Theory]
    [InlineData("2026-07-01T10:00:00")]
    [InlineData("2026-07-01T10:00")]
    [InlineData("2026-07-01T10:00:00.5")]
    [InlineData("2026-07-01")]
    [InlineData("2026-07-01Z")]
    [InlineData("01.07.2026 10:00 +02:00")]
    [InlineData("2026-07-01T10:00:00+25:00")]
    [InlineData("")]
    public void Deserialize_ValueWithoutOffsetOrNotIso_ThrowsExplainingTheFormat(string text)
    {
        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Instants>($$"""{"at":"{{text}}"}""", Options));

        exception.Message.ShouldContain("with a UTC offset or 'Z'");
        exception.Path.ShouldBe("$.at");
    }

    [Theory]
    [InlineData("1782892800")]
    [InlineData("null")]
    [InlineData("true")]
    public void Deserialize_NonStringToken_Throws(string token)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Instants>($$"""{"at":{{token}}}""", Options));
    }

    [Fact]
    public void Deserialize_Nullable_ReadsNullAndValuesWithAnOffset()
    {
        var withNull = JsonSerializer.Deserialize<Instants>("""{"at":"2026-07-01T08:00:00Z","optional":null}""", Options);
        var withValue = JsonSerializer.Deserialize<Instants>("""{"at":"2026-07-01T08:00:00Z","optional":"2026-07-01T10:00:00+02:00"}""", Options);

        withNull.ShouldNotBeNull().Optional.ShouldBeNull();
        withValue.ShouldNotBeNull().Optional.ShouldBe(EightUtc);
    }

    [Fact]
    public void Deserialize_NullableWithoutOffset_Throws()
    {
        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<Instants>("""{"at":"2026-07-01T08:00:00Z","optional":"2026-07-01T10:00:00"}""", Options));

        exception.Path.ShouldBe("$.optional");
    }

    [Theory]
    [InlineData(0, "\"2026-07-01T08:00:00+00:00\"")]
    [InlineData(2, "\"2026-07-01T10:00:00+02:00\"")]
    [InlineData(-5, "\"2026-07-01T03:00:00-05:00\"")]
    public void Serialize_WritesIsoWithTheValuesOffset(int offsetHours, string expected)
    {
        var instant = EightUtc.ToOffset(TimeSpan.FromHours(offsetHours));

        JsonSerializer.Serialize(instant, Options).ShouldBe(expected);
        JsonSerializer.Serialize(instant).ShouldBe(expected, "the converter writes what System.Text.Json writes");
    }

    [Fact]
    public void Serialize_NullableWritesNullOrTheInstant()
    {
        JsonSerializer.Serialize(new Instants { At = EightUtc }, Options)
            .ShouldBe("""{"at":"2026-07-01T08:00:00+00:00","optional":null}""");
        JsonSerializer.Serialize(new Instants { At = EightUtc, Optional = EightUtc }, Options)
            .ShouldBe("""{"at":"2026-07-01T08:00:00+00:00","optional":"2026-07-01T08:00:00+00:00"}""");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5_000_000)]
    [InlineData(1_234_567)]
    public void DictionaryKeys_AreWrittenAsValuesAreAndRoundTrip(long ticks)
    {
        var instant = EightUtc.ToOffset(TimeSpan.FromHours(2)).AddTicks(ticks);
        var value = new Dictionary<DateTimeOffset, int> { [instant] = 1 };

        var json = JsonSerializer.Serialize(value, Options);

        json.ShouldBe($$"""{{{JsonSerializer.Serialize(instant)}}:1}""");
        var read = JsonSerializer.Deserialize<Dictionary<DateTimeOffset, int>>(json, Options).ShouldNotBeNull();
        read.Keys.ShouldHaveSingleItem().Offset.ShouldBe(TimeSpan.FromHours(2));
        read.ShouldBe(value);
    }

    [Theory]
    [InlineData("2026-07-01T10:00:00")]
    [InlineData("2026-07-01")]
    public void DictionaryKeys_WithoutOffset_Throw(string key)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Dictionary<DateTimeOffset, int>>($$"""{"{{key}}":1}""", Options))
            .Message.ShouldContain("with a UTC offset or 'Z'");
    }

    private sealed class Instants
    {
        public DateTimeOffset At { get; set; }

        public DateTimeOffset? Optional { get; set; }
    }
}
