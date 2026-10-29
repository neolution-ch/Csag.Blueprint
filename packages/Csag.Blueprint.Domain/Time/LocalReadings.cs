namespace Csag.Blueprint.Domain.Time;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Derives instants from local readings for <see cref="PlaceTime"/>, <see cref="ZonedPlaceTime"/> and
/// <see cref="ViewerTime"/>, so all three follow the daylight saving rules of <see cref="ZonedTime.TryToInstant"/>.
/// </summary>
internal static class LocalReadings
{
    /// <summary>
    /// Derives the instant of a reading in a zone.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the clock.</param>
    /// <param name="reading">The reading, with <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <param name="at">The instant, in UTC, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the reading exists in the zone.</returns>
    public static bool TryDerive(DateTime local, TimeZoneInfo zone, out DateTime reading, out DateTimeOffset at)
    {
        reading = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return ZonedTime.TryToInstant(reading, zone, out at);
    }

    /// <summary>
    /// Derives the instant of a reading in the zone an IANA id names.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="timeZoneId">The IANA id of the zone of the clock.</param>
    /// <param name="reading">The reading, with <see cref="DateTimeKind.Unspecified"/>.</param>
    /// <param name="at">The instant, in UTC, when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="ZonedTime.TryFindIanaTimeZone"/> resolves the id and the reading
    /// exists in its zone.
    /// </returns>
    public static bool TryDerive(DateTime local, [NotNullWhen(true)] string? timeZoneId, out DateTime reading, out DateTimeOffset at)
    {
        if (!ZonedTime.TryFindIanaTimeZone(timeZoneId, out var zone))
        {
            reading = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            at = default;
            return false;
        }

        return TryDerive(local, zone, out reading, out at);
    }

    /// <summary>
    /// Derives the instant of a reading in a zone, failing when the reading does not exist there.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the clock.</param>
    /// <returns>The reading, with <see cref="DateTimeKind.Unspecified"/>, and its instant in UTC.</returns>
    /// <exception cref="ArgumentException">The reading falls in a spring-forward gap of the zone.</exception>
    public static (DateTime Reading, DateTimeOffset At) Derive(DateTime local, TimeZoneInfo zone)
    {
        if (!TryDerive(local, zone, out var reading, out var at))
        {
            throw new ArgumentException(
                $"The local reading {Format(reading)} does not exist in {zone.Id}: clocks skip it when they spring forward.",
                nameof(local));
        }

        return (reading, at);
    }

    /// <summary>
    /// Derives the instant of a reading in a zone. A reading that falls in a spring-forward gap is moved
    /// forward by the length of the gap, to the reading a clock that was not changed would show.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the clock.</param>
    /// <returns>
    /// The reading, with <see cref="DateTimeKind.Unspecified"/> and moved when it fell in a gap, its instant in
    /// UTC, and whether it was moved.
    /// </returns>
    /// <exception cref="InvalidOperationException">The reading has no instant even when moved.</exception>
    public static (DateTime Reading, DateTimeOffset At, bool Shifted) DeriveShiftingGap(DateTime local, TimeZoneInfo zone)
    {
        if (TryDerive(local, zone, out var reading, out var at))
        {
            return (reading, at, false);
        }

        // The clocks jumped from the start of the gap straight to its end. Moving the reading by the jump keeps
        // its distance from the transition: 02:30 in a gap from 02:00 to 03:00 becomes 03:30.
        var transition = ZonedTime.FindGapTransition(reading, zone);
        if (transition > DateTimeOffset.MinValue)
        {
            var gap = zone.GetUtcOffset(transition) - zone.GetUtcOffset(transition.AddTicks(-1));
            if (gap > TimeSpan.Zero
                && reading.Ticks <= DateTime.MaxValue.Ticks - gap.Ticks
                && ZonedTime.TryToInstant(reading + gap, zone, out at))
            {
                return (reading + gap, at, true);
            }
        }

        throw new InvalidOperationException($"The local reading {Format(reading)} has no instant in {zone.Id}.");
    }

    /// <summary>
    /// Resolves the IANA id of a zone that a caller passes in.
    /// </summary>
    /// <param name="timeZoneId">The IANA id.</param>
    /// <param name="parameterName">The name of the caller's parameter that holds the id.</param>
    /// <returns>The zone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="timeZoneId"/> is not an IANA id this host can resolve (<see cref="ZonedTime.TryFindIanaTimeZone"/>).
    /// </exception>
    public static TimeZoneInfo FindZone(string timeZoneId, string parameterName)
    {
        if (timeZoneId is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return ZonedTime.TryFindIanaTimeZone(timeZoneId, out var zone)
            ? zone
            : throw new ArgumentException("The time zone id is not an IANA time zone id this host can resolve.", parameterName);
    }

    /// <summary>
    /// Resolves the IANA id stored with a reading.
    /// </summary>
    /// <param name="timeZoneId">The stored IANA id.</param>
    /// <returns>The zone.</returns>
    /// <exception cref="TimeZoneNotFoundException">This host cannot resolve the id.</exception>
    public static TimeZoneInfo FindStoredZone(string timeZoneId)
    {
        return ZonedTime.TryFindIanaTimeZone(timeZoneId, out var zone)
            ? zone
            : throw new TimeZoneNotFoundException($"The time zone '{timeZoneId}' is not an IANA time zone id this host can resolve.");
    }

    private static string Format(DateTime reading) => reading.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
