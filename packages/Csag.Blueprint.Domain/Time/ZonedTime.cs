namespace Csag.Blueprint.Domain.Time;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Converts between instants and the clock readings of a place, using <see cref="TimeZoneInfo"/>
/// zones identified by IANA ids.
/// </summary>
/// <remarks>
/// <para>
/// An instant is a <see cref="DateTimeOffset"/>; every instant this class returns carries a zero
/// offset (UTC). A local reading is a <see cref="DateTime"/> holding the clock reading on a wall at
/// the place; its <see cref="DateTime.Kind"/> is ignored, and readings this class returns are
/// <see cref="DateTimeKind.Unspecified"/>.
/// </para>
/// <para>
/// Daylight saving time makes the mapping from readings to instants partial and non-unique: when
/// clocks spring forward, the skipped readings do not exist, and when they fall back, the repeated
/// readings happen twice. A reading that does not exist has no instant
/// (<see cref="TryToInstant"/> returns <see langword="false"/>), and a repeated reading resolves to
/// its earlier occurrence.
/// </para>
/// </remarks>
public static class ZonedTime
{
    /// <summary>
    /// Converts a local reading in a zone to the instant it denotes.
    /// </summary>
    /// <param name="localReading">The clock reading at the place. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <param name="instant">The instant, in UTC, when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the reading exists in the zone; <see langword="false"/> when it falls in a
    /// spring-forward gap, or when its instant lies outside the range of <see cref="DateTimeOffset"/>. A reading
    /// that happens twice (a fall-back overlap) resolves to its earlier occurrence.
    /// </returns>
    public static bool TryToInstant(DateTime localReading, TimeZoneInfo zone, out DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var reading = DateTime.SpecifyKind(localReading, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(reading))
        {
            instant = default;
            return false;
        }

        // Of the two offsets a repeated reading has, the larger one gives the earlier instant.
        var offset = zone.IsAmbiguousTime(reading)
            ? zone.GetAmbiguousTimeOffsets(reading).Max()
            : zone.GetUtcOffset(reading);

        return TryCreateUtcInstant(reading.Ticks - offset.Ticks, out instant);
    }

    /// <summary>
    /// Converts an instant to the local reading a clock in the zone shows at that instant.
    /// </summary>
    /// <param name="instant">The instant.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <returns>The local reading, with <see cref="DateTimeKind.Unspecified"/>.</returns>
    public static DateTime ToLocalReading(DateTimeOffset instant, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return TimeZoneInfo.ConvertTime(instant, zone).DateTime;
    }

    /// <summary>
    /// Gets today's date in the zone, which differs from today's UTC date for part of every day in
    /// any zone other than UTC.
    /// </summary>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <returns>The calendar date a clock in the zone shows now.</returns>
    public static DateOnly Today(TimeProvider timeProvider, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(zone);

        return DateOnly.FromDateTime(ToLocalReading(timeProvider.GetUtcNow(), zone));
    }

    /// <summary>
    /// Gets the instants a calendar day in the zone spans, as the half-open range
    /// [<c>Start</c>, <c>End</c>). The day is 23 or 25 hours long on the days clocks change.
    /// </summary>
    /// <remarks>
    /// When local midnight does not exist (zones whose clocks spring forward at midnight), the day starts
    /// at the first instant whose local reading falls on that date. When midnight happens twice, the day
    /// starts at its earlier occurrence. A start or end outside the range of <see cref="DateTimeOffset"/>
    /// is clamped to <see cref="DateTimeOffset.MinValue"/> or <see cref="DateTimeOffset.MaxValue"/>.
    /// </remarks>
    /// <param name="date">The calendar date.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <returns>The start of the day (inclusive) and the start of the next day (exclusive), in UTC.</returns>
    public static (DateTimeOffset Start, DateTimeOffset End) DayRange(DateOnly date, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var start = StartOfDay(date, zone);
        var end = date == DateOnly.MaxValue ? DateTimeOffset.MaxValue : StartOfDay(date.AddDays(1), zone);
        return (start, end);
    }

    /// <summary>
    /// Finds a time zone by its IANA id, rejecting Windows ids such as <c>W. Europe Standard Time</c> that
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> also accepts.
    /// </summary>
    /// <remarks>
    /// The id must match the zone's id exactly, including case, so one zone is never stored or compared
    /// under two spellings. Backward links such as <c>UTC</c> and <c>Asia/Calcutta</c>, which browsers may
    /// report, are accepted. Resolution needs the host's time zone data (the <c>tzdata</c> package on Linux,
    /// ICU on Windows); without it every id is rejected.
    /// </remarks>
    /// <param name="id">The IANA zone id, for example <c>Europe/Zurich</c>.</param>
    /// <param name="zone">The zone when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="id"/> is an IANA id this host can resolve.</returns>
    public static bool TryFindIanaTimeZone(string? id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        // The lookup also resolves Windows ids (from the registry on Windows, through ICU on Linux) and
        // matches ids case-insensitively; HasIanaId and the exact comparison refuse both.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found)
            || !found.HasIanaId
            || !string.Equals(found.Id, id, StringComparison.Ordinal))
        {
            return false;
        }

        zone = found;
        return true;
    }

    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        if (!zone.IsInvalidTime(midnight))
        {
            return TryToInstant(midnight, zone, out var instant)
                ? instant
                : ClampToRange(midnight.Ticks - zone.GetUtcOffset(midnight).Ticks);
        }

        // Midnight falls in a spring-forward gap, so the day starts at the transition: the first instant
        // whose local reading is at or after midnight. Local readings only move forward around a gap, so
        // a binary search over the surrounding two days finds it.
        var low = Math.Max(midnight.Ticks - TimeSpan.TicksPerDay, DateTime.MinValue.Ticks);
        var high = Math.Min(midnight.Ticks + TimeSpan.TicksPerDay, DateTime.MaxValue.Ticks);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (ToLocalReading(new DateTimeOffset(middle, TimeSpan.Zero), zone) >= midnight)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        // Windows adjustment rules cannot express a transition at 24:00 and place it at 23:59:59.999
        // instead. Real transitions fall on whole seconds, so rounding up recovers the instant meant.
        var partialSecond = low % TimeSpan.TicksPerSecond;
        if (partialSecond != 0)
        {
            low = Math.Min(low - partialSecond + TimeSpan.TicksPerSecond, DateTime.MaxValue.Ticks);
        }

        return new DateTimeOffset(low, TimeSpan.Zero);
    }

    private static bool TryCreateUtcInstant(long utcTicks, out DateTimeOffset instant)
    {
        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
        {
            instant = default;
            return false;
        }

        instant = new DateTimeOffset(utcTicks, TimeSpan.Zero);
        return true;
    }

    private static DateTimeOffset ClampToRange(long utcTicks)
    {
        if (utcTicks < DateTime.MinValue.Ticks)
        {
            return DateTimeOffset.MinValue;
        }

        return utcTicks > DateTime.MaxValue.Ticks ? DateTimeOffset.MaxValue : new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }
}
