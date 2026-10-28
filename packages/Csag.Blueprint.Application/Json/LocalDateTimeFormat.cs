namespace Csag.Blueprint.Application.Json;

using System.Globalization;

/// <summary>
/// The text form of a local reading: <c>yyyy-MM-ddTHH:mm:ss</c> when written, <c>yyyy-MM-ddTHH:mm:ss</c> or
/// <c>yyyy-MM-ddTHH:mm</c> when read, never with a fraction, a UTC offset or <c>Z</c>. Every place that reads
/// or writes a <see cref="DateTime"/> as text (JSON bodies, query, route and form values, table view filters)
/// uses it, so they accept exactly the same values.
/// </summary>
public static class LocalDateTimeFormat
{
    private const string InvalidMessage =
        "The value must be a local date and time in the format yyyy-MM-ddTHH:mm:ss or yyyy-MM-ddTHH:mm.";

    private const string OffsetMessage =
        "The value must be a local date and time without a UTC offset or 'Z', in the format yyyy-MM-ddTHH:mm:ss " +
        "or yyyy-MM-ddTHH:mm: send the clock reading at the place, not an instant.";

    private const string WriteFormat = "yyyy-MM-dd'T'HH:mm:ss";

    private static readonly string[] ReadFormats = [WriteFormat, "yyyy-MM-dd'T'HH:mm"];

    /// <summary>
    /// Formats a local reading. The <see cref="DateTime.Kind"/> is ignored and fractional seconds are dropped.
    /// </summary>
    /// <param name="reading">The local reading.</param>
    /// <returns>The reading as <c>yyyy-MM-ddTHH:mm:ss</c>.</returns>
    public static string Format(DateTime reading) => reading.ToString(WriteFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a local reading in the format <c>yyyy-MM-ddTHH:mm:ss</c> or <c>yyyy-MM-ddTHH:mm</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="reading">The reading, with <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <returns><see langword="true"/> when the text is a local reading in one of the two formats.</returns>
    public static bool TryParse(string? text, out DateTime reading)
    {
        if (DateTime.TryParseExact(text, ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out reading))
        {
            reading = DateTime.SpecifyKind(reading, DateTimeKind.Unspecified);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the message that explains why a value is not a local reading; it says so specifically when the time
    /// part ends in <c>Z</c> or carries a numeric offset, the most common mistake.
    /// </summary>
    /// <param name="text">The value that failed to parse.</param>
    /// <returns>The message.</returns>
    public static string GetInvalidMessage(string? text)
    {
        var timeStart = text?.IndexOf('T', StringComparison.OrdinalIgnoreCase) ?? -1;
        if (timeStart < 0)
        {
            return InvalidMessage;
        }

        var time = text.AsSpan(timeStart + 1);
        return time.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || time.IndexOfAny('+', '-') >= 0
            ? OffsetMessage
            : InvalidMessage;
    }
}
