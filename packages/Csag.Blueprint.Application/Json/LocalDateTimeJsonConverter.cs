namespace Csag.Blueprint.Application.Json;

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
    /// <inheritdoc/>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(LocalDateTimeFormat.GetInvalidMessage(null));
        }

        return Parse(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(LocalDateTimeFormat.Format(value));
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

        writer.WritePropertyName(LocalDateTimeFormat.Format(value));
    }

    private static DateTime Parse(string? text)
    {
        return LocalDateTimeFormat.TryParse(text, out var reading)
            ? reading
            : throw new JsonException(LocalDateTimeFormat.GetInvalidMessage(text));
    }
}
