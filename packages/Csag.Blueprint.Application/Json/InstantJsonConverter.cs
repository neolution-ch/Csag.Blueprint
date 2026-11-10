namespace Csag.Blueprint.Application.Json;

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serializes a <see cref="DateTimeOffset"/> as an instant: ISO 8601 with a UTC offset or <c>Z</c>
/// (<see cref="InstantFormat"/>). Applies to <see cref="Nullable{DateTimeOffset}"/> and to dictionary keys as well.
/// </summary>
/// <remarks>
/// System.Text.Json reads a value without an offset, and a date on its own, in the server's zone. This converter
/// refuses such a value instead, and the <see cref="JsonException"/> it throws reaches the client as a 400
/// validation error. Values with an offset are read by System.Text.Json's own ISO 8601 parser and keep their
/// offset; values are written as System.Text.Json writes them.
/// </remarks>
public sealed class InstantJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string InvalidMessage =
        "The value must be an instant in ISO 8601 with a UTC offset or 'Z', such as 2026-07-01T10:00:00+02:00.";

    // The text Utf8JsonWriter writes for a DateTimeOffset value: no trailing zeros in the fraction, and no
    // fraction at all when it is zero.
    private const string PropertyNameFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFzzz";

    /// <inheritdoc/>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !InstantFormat.HasOffset(reader.GetString())
            || !reader.TryGetDateTimeOffset(out var instant))
        {
            throw new JsonException(InvalidMessage);
        }

        return instant;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value);
    }

    /// <inheritdoc/>
    public override DateTimeOffset ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return InstantFormat.TryParse(reader.GetString(), out var instant)
            ? instant
            : throw new JsonException(InvalidMessage);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // The name holds only digits, '-', ':', 'T', '.' and the offset's sign. The default encoder would escape a
        // '+' as \u002B; the relaxed one leaves it as System.Text.Json writes DateTimeOffset values and keys.
        var name = value.ToString(PropertyNameFormat, CultureInfo.InvariantCulture);
        writer.WritePropertyName(JsonEncodedText.Encode(name, JavaScriptEncoder.UnsafeRelaxedJsonEscaping));
    }
}
