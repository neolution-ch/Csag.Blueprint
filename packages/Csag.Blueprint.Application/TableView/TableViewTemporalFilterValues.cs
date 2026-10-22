namespace Csag.Blueprint.Application.TableView;

using System.Globalization;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Parses the temporal values of table view filters, and turns a range of calendar dates into the
/// bounds that range means on a date, local reading or instant column.
/// </summary>
/// <remarks>
/// Filter values are untrusted request input, so parsing is strict (ISO 8601 only, never the server's
/// culture or zone) and every failure is reported rather than thrown.
/// </remarks>
internal static class TableViewTemporalFilterValues
{
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] LocalReadingFormats = ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm"];

    private static readonly string[] OffsetFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mmzzz",
    ];

    private static readonly string[] UtcFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm'Z'",
    ];

    /// <summary>
    /// Parses a calendar date in the format <c>yyyy-MM-dd</c>, ignoring surrounding whitespace.
    /// </summary>
    /// <param name="value">The filter value.</param>
    /// <param name="date">The parsed date.</param>
    /// <returns><see langword="true"/> when the value is a valid date.</returns>
    public static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>
    /// Parses a single temporal value for a column of the given type: a <see cref="DateOnly"/> from
    /// <c>yyyy-MM-dd</c>; a local reading (<see cref="DateTime"/>) from <c>yyyy-MM-ddTHH:mm[:ss]</c> without
    /// an offset; an instant (<see cref="DateTimeOffset"/>) from a value with an offset or <c>Z</c>, or from
    /// a local reading in <paramref name="timeZone"/>.
    /// </summary>
    /// <param name="value">The filter value.</param>
    /// <param name="targetType">The column's type, nullable or not.</param>
    /// <param name="timeZone">The zone an instant given without an offset is read in.</param>
    /// <param name="result">The parsed value, of the column's underlying type.</param>
    /// <returns><see langword="true"/> when the value parses for the column's type.</returns>
    public static bool TryParseValue(string value, Type targetType, TimeZoneInfo timeZone, out object? result)
    {
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var text = value.Trim();
        result = null;

        if (type == typeof(DateOnly) && TryParseDate(text, out var date))
        {
            result = date;
        }
        else if (type == typeof(DateTime) && TryParseLocalReading(text, out var reading))
        {
            result = reading;
        }
        else if (type == typeof(DateTimeOffset) && TryParseInstant(text, timeZone, out var instant))
        {
            result = instant;
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the bounds a range of calendar dates puts on a column of the given type. A
    /// <see cref="DateOnly"/> column matches [from, to] inclusive. A local reading (<see cref="DateTime"/>)
    /// matches readings at or after <c>from</c> 00:00 and before the day after <c>to</c> 00:00, with no
    /// zone involved. An instant (<see cref="DateTimeOffset"/>) matches instants at or after the start of
    /// <c>from</c> and before the start of the day after <c>to</c>, as those days begin in
    /// <paramref name="timeZone"/>.
    /// </summary>
    /// <param name="targetType">The column's type, nullable or not.</param>
    /// <param name="from">The first date of the range, or null for no lower bound.</param>
    /// <param name="to">The last date of the range, or null for no upper bound.</param>
    /// <param name="timeZone">The zone whose days an instant range spans.</param>
    /// <param name="bounds">The bounds, as values of the column's underlying type.</param>
    /// <returns><see langword="false"/> when the column's type is not a date, local reading or instant.</returns>
    public static bool TryGetDayBounds(Type targetType, DateOnly? from, DateOnly? to, TimeZoneInfo timeZone, out TableViewDayBounds bounds)
    {
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var dayAfterTo = to is { } last && last < DateOnly.MaxValue ? last.AddDays(1) : (DateOnly?)null;

        if (type == typeof(DateOnly))
        {
            bounds = new TableViewDayBounds(from, to, IsUpperInclusive: true);
        }
        else if (type == typeof(DateTime))
        {
            bounds = new TableViewDayBounds(
                from?.ToDateTime(TimeOnly.MinValue),
                dayAfterTo?.ToDateTime(TimeOnly.MinValue),
                IsUpperInclusive: false);
        }
        else if (type == typeof(DateTimeOffset))
        {
            bounds = new TableViewDayBounds(
                from is { } first ? ZonedTime.DayRange(first, timeZone).Start : null,
                dayAfterTo is { } next ? ZonedTime.DayRange(next, timeZone).Start : null,
                IsUpperInclusive: false);
        }
        else
        {
            bounds = default;
            return false;
        }

        return true;
    }

    private static bool TryParseLocalReading(string text, out DateTime reading)
    {
        if (DateTime.TryParseExact(text, LocalReadingFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out reading))
        {
            reading = DateTime.SpecifyKind(reading, DateTimeKind.Unspecified);
            return true;
        }

        return false;
    }

    private static bool TryParseInstant(string text, TimeZoneInfo timeZone, out DateTimeOffset instant)
    {
        if (DateTimeOffset.TryParseExact(text, OffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out instant)
            || DateTimeOffset.TryParseExact(text, UtcFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out instant))
        {
            instant = instant.ToUniversalTime();
            return true;
        }

        return TryParseLocalReading(text, out var reading) && ZonedTime.TryToInstant(reading, timeZone, out instant);
    }
}

