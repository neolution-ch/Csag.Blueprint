namespace Csag.Blueprint.Application.Json;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Serializes a <see cref="DateTime"/> as a local reading: the clock reading at a place, with no
/// UTC offset. Writes <c>yyyy-MM-ddTHH:mm:ss</c> and reads <c>yyyy-MM-ddTHH:mm:ss</c> or
/// <c>yyyy-MM-ddTHH:mm</c>. Applies to <see cref="Nullable{DateTime}"/> as well.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DateTime"/> in a contract always means a local reading; an instant is a
/// <see cref="DateTimeOffset"/>. Reading therefore refuses a value with a UTC offset or <c>Z</c>
/// instead of silently converting it to some zone, and the <see cref="JsonException"/> it throws
/// reaches the client as a 400 validation error. Values read have
/// <see cref="DateTimeKind.Unspecified"/>.
/// </para>
/// <para>
/// Writing ignores <see cref="DateTime.Kind"/> and drops fractional seconds, so a
/// <see cref="DateTime"/> that holds an instant (such as <see cref="DateTime.UtcNow"/>) loses its
/// meaning on the wire. Use <see cref="DateTimeOffset"/> for those.
/// </para>
/// </remarks>
public sealed class LocalDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string WriteFormat = "yyyy-MM-dd'T'HH:mm:ss";

    private const string FormatMessage =
        "The value must be a local date and time in the format yyyy-MM-ddTHH:mm:ss or yyyy-MM-ddTHH:mm.";

    private const string OffsetMessage =
        "The value must be a local date and time without a UTC offset or 'Z', in the format yyyy-MM-ddTHH:mm:ss " +
        "or yyyy-MM-ddTHH:mm: send the clock reading at the place, not an instant.";

    private static readonly string[] ReadFormats = [WriteFormat, "yyyy-MM-dd'T'HH:mm"];

    /// <inheritdoc/>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(FormatMessage);
        }

        return Parse(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString(WriteFormat, CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public override DateTime ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Parse(reader.GetString());
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WritePropertyName(value.ToString(WriteFormat, CultureInfo.InvariantCulture));
    }

    private static DateTime Parse(string? text)
    {
        if (DateTime.TryParseExact(text, ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }

        throw new JsonException(HasOffset(text) ? OffsetMessage : FormatMessage);
    }

    /// <summary>
    /// Determines whether the time part of an ISO 8601 value ends in <c>Z</c> or a numeric offset.
    /// </summary>
    private static bool HasOffset(string? text)
    {
        var timeStart = text?.IndexOf('T', StringComparison.OrdinalIgnoreCase) ?? -1;
        if (timeStart < 0)
        {
            return false;
        }

        var time = text.AsSpan(timeStart + 1);
        return time.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || time.IndexOfAny('+', '-') >= 0;
    }
}
