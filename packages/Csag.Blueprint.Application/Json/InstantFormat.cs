namespace Csag.Blueprint.Application.Json;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

/// <summary>
/// The text form of an instant: ISO 8601 with a UTC offset or <c>Z</c>, such as <c>2026-07-01T10:00:00+02:00</c>
/// or <c>2026-07-01T08:00:00Z</c>, as System.Text.Json's own ISO 8601 parser reads it. A date and time without an
/// offset, which that parser would read in the server's zone, and a date on its own are not instants. JSON bodies
/// (<see cref="InstantJsonConverter"/>) and query, route, form and header values use it, so they accept exactly
/// the same values.
/// </summary>
public static class InstantFormat
{
    /// <summary>
    /// Parses an instant with a UTC offset or <c>Z</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="instant">The instant, with the offset the text carries.</param>
    /// <returns><see langword="true"/> when the text is an instant with a UTC offset or <c>Z</c>.</returns>
    public static bool TryParse(string? text, out DateTimeOffset instant)
    {
        instant = default;
        if (!HasOffset(text))
        {
            return false;
        }

        // System.Text.Json exposes its ISO 8601 parser only through Utf8JsonReader, so the text is read as a
        // JSON string.
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes($"\"{JsonEncodedText.Encode(text)}\""));
        return reader.Read() && reader.TryGetDateTimeOffset(out instant);
    }

    /// <summary>
    /// Determines whether the time part of a value ends in <c>Z</c> or carries a numeric offset. A value without
    /// a time part has none.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <returns><see langword="true"/> when the value has a time part with a UTC offset or <c>Z</c>.</returns>
    internal static bool HasOffset([NotNullWhen(true)] string? text)
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
