namespace Csag.Blueprint.Application.UnitTests.Json;

using System.Text.Json;
using Csag.Blueprint.Application.Json;

/// <summary>
/// Pins the wire format of <see cref="DateTime"/> under <see cref="BlueprintJsonOptions.Default"/>: a local
/// reading <c>yyyy-MM-ddTHH:mm:ss</c> with no offset, read back with <see cref="DateTimeKind.Unspecified"/>.
/// Uses the production options so the tests fail if the converter is ever unregistered.
/// </summary>
public sealed class LocalDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = BlueprintJsonOptions.Default;

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Serialize_WritesReadingWithoutOffsetWhateverTheKind(DateTimeKind kind)
    {
        var value = new Reading { At = new DateTime(2026, 10, 26, 10, 0, 0, kind) };

        JsonSerializer.Serialize(value, Options).ShouldBe("""{"at":"2026-10-26T10:00:00","optional":null}""");
    }

    [Fact]
    public void Serialize_DropsFractionalSeconds()
    {
        var value = new Reading { At = new DateTime(2026, 10, 26, 10, 0, 5, 750, DateTimeKind.Unspecified) };

        JsonSerializer.Serialize(value, Options).ShouldContain("\"2026-10-26T10:00:05\"");
    }

    [Fact]
    public void Serialize_NullableWithValue_WritesReading()
    {
        var value = new Reading { Optional = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified) };

        JsonSerializer.Serialize(value, Options).ShouldContain("\"optional\":\"2026-10-26T10:00:00\"");
    }

    [Theory]
    [InlineData("2026-10-26T10:00:00")]
    [InlineData("2026-10-26T10:00")]
    public void Deserialize_ReadingWithOrWithoutSeconds_ReadsUnspecifiedReading(string text)
    {
        var value = JsonSerializer.Deserialize<Reading>($$"""{"at":"{{text}}","optional":"{{text}}"}""", Options);

        value.ShouldNotBeNull();
        value.At.ShouldBe(new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified));
        value.At.Kind.ShouldBe(DateTimeKind.Unspecified);
        value.Optional.ShouldBe(new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Deserialize_NullForNullable_ReadsNull()
    {
        var value = JsonSerializer.Deserialize<Reading>("""{"at":"2026-10-26T10:00:00","optional":null}""", Options);

        value.ShouldNotBeNull();
        value.Optional.ShouldBeNull();
    }

    [Theory]
    [InlineData("2026-10-26T10:00:00Z")]
    [InlineData("2026-10-26T10:00:00+01:00")]
    [InlineData("2026-10-26T10:00:00-05:00")]
    [InlineData("2026-10-26T10:00Z")]
    public void Deserialize_ValueWithOffset_ThrowsExplainingThatReadingsCarryNoOffset(string text)
    {
        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Reading>($$"""{"at":"{{text}}"}""", Options));

        exception.Message.ShouldContain("without a UTC offset or 'Z'");
        exception.Path.ShouldBe("$.at");
    }

    [Theory]
    [InlineData("2026-10-26")]
    [InlineData("2026-10-26 10:00:00")]
    [InlineData("2026-10-26T10:00:00.5")]
    [InlineData("26.10.2026 10:00")]
    [InlineData("2026-02-30T10:00:00")]
    [InlineData("")]
    public void Deserialize_ValueInAnotherFormat_ThrowsNamingTheFormat(string text)
    {
        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Reading>($$"""{"at":"{{text}}"}""", Options));

        exception.Message.ShouldContain("yyyy-MM-ddTHH:mm:ss");
    }

    [Fact]
    public void Deserialize_NonStringToken_Throws()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Reading>("""{"at":1761472800}""", Options));
    }

    [Fact]
    public void DictionaryKeys_RoundTripAsReadings()
    {
        var value = new Dictionary<DateTime, int> { [new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified)] = 1 };

        var json = JsonSerializer.Serialize(value, Options);

        json.ShouldBe("""{"2026-10-26T10:00:00":1}""");
        JsonSerializer.Deserialize<Dictionary<DateTime, int>>(json, Options).ShouldBe(value);
    }

    [Fact]
    public void DateTimeOffset_IsUnaffected()
    {
        var instant = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

        JsonSerializer.Serialize(instant, Options).ShouldBe("\"2026-10-26T09:00:00+00:00\"");
    }

    private sealed class Reading
    {
        public DateTime At { get; set; }

        public DateTime? Optional { get; set; }
    }
}
