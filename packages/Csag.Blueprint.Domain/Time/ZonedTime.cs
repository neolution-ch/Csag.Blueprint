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
    /// The longest id accepted as well-formed. The longest id in the tz database has 32 characters; the
    /// limit leaves room for new ones while keeping arbitrary input out of lookups and logs.
    /// </summary>
    private const int MaxIdLength = 64;

    /// <summary>
    /// Files in a tz database directory that are not zones: the host's own zone, the template for POSIX
    /// TZ strings, and the placeholder zone of the distribution build.
    /// </summary>
    private static readonly string[] NonZoneIds = ["localtime", "posixrules", "Factory"];

    /// <summary>
    /// Directories in some tz database installations that repeat every zone with other semantics: POSIX
    /// time without leap seconds, and time with them.
    /// </summary>
    private static readonly string[] NonZonePrefixes = ["posix/", "right/"];

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

        // On Linux, IsInvalidTime and GetUtcOffset miss past gaps in zones whose current rules have no
        // daylight saving time (America/Chihuahua before 2022), while converting an instant back to a
        // reading uses every recorded transition. A reading only exists when its instant reads back as it.
        if (!TryCreateUtcInstant(reading.Ticks - offset.Ticks, out instant) || ToLocalReading(instant, zone) != reading)
        {
            instant = default;
            return false;
        }

        return true;
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
    /// Determines whether an id has the form of an IANA zone id, whether or not this host's time zone data
    /// knows it: one or more names of ASCII letters, digits, <c>_</c>, <c>+</c> and <c>-</c>, separated by
    /// single slashes, such as <c>Europe/Zurich</c>, <c>America/Argentina/Buenos_Aires</c>, <c>Etc/GMT+5</c>
    /// or <c>UTC</c>, at most 64 characters long. The tz database's files that are not zones (<c>localtime</c>, <c>posixrules</c>,
    /// <c>Factory</c>, and the <c>posix/</c> and <c>right/</c> trees) are not ids.
    /// </summary>
    /// <remarks>
    /// Use it to tell an id this host cannot resolve, which newer time zone data elsewhere (a browser) may know,
    /// from a value that is no id at all.
    /// </remarks>
    /// <param name="id">The candidate id.</param>
    /// <returns><see langword="true"/> when <paramref name="id"/> has the form of an IANA zone id.</returns>
    public static bool IsWellFormedIanaTimeZoneId([NotNullWhen(true)] string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || id[0] == '/' || id[^1] == '/')
        {
            return false;
        }

        for (var index = 0; index < id.Length; index++)
        {
            var character = id[index];
            var isNameCharacter = char.IsAsciiLetterOrDigit(character) || character is '_' or '+' or '-';
            var isSeparator = character == '/' && id[index - 1] != '/';
            if (!isNameCharacter && !isSeparator)
            {
                return false;
            }
        }

        return !Array.Exists(NonZoneIds, nonZone => string.Equals(id, nonZone, StringComparison.OrdinalIgnoreCase))
            && !Array.Exists(NonZonePrefixes, prefix => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a time zone by its IANA id, rejecting Windows ids such as <c>W. Europe Standard Time</c> that
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> also accepts.
    /// </summary>
    /// <remarks>
    /// The id must be well-formed (<see cref="IsWellFormedIanaTimeZoneId"/>) and match the zone's id exactly,
    /// including case, so one zone is never stored or compared under two spellings. Backward links such as
    /// <c>UTC</c> and <c>Asia/Calcutta</c>, which browsers may report, are accepted when the host's time zone
    /// data has them: Debian and Ubuntu ship most backward links (<c>Asia/Calcutta</c>, <c>Asia/Saigon</c>,
    /// <c>America/Buenos_Aires</c>, <c>Europe/Kiev</c>, <c>US/Eastern</c> and others) in a separate
    /// <c>tzdata-legacy</c> package, while Alpine's <c>tzdata</c> has them all. Resolution needs that data (the
    /// <c>tzdata</c> package on Linux, ICU on Windows); without it every id is rejected.
    /// </remarks>
    /// <param name="id">The IANA zone id, for example <c>Europe/Zurich</c>.</param>
    /// <param name="zone">The zone when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="id"/> is an IANA id this host can resolve.</returns>
    public static bool TryFindIanaTimeZone(string? id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;

        // On Linux the lookup reads the id as a path under the zoneinfo directory, so it also resolves
        // spellings no browser accepts, such as Europe//Zurich, localtime or right/UTC.
        if (!IsWellFormedIanaTimeZoneId(id))
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
        if (TryToInstant(midnight, zone, out var instant))
        {
            return instant;
        }

        var estimate = midnight.Ticks - zone.GetUtcOffset(midnight).Ticks;
        if (estimate < DateTime.MinValue.Ticks || estimate > DateTime.MaxValue.Ticks)
        {
            return ClampToRange(estimate);
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
