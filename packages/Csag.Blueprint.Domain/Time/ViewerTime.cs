namespace Csag.Blueprint.Domain.Time;

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A viewer time: the clock reading an organiser set in their own zone, the IANA id of that zone, and the
/// instant the reading denotes, which each viewer sees in their own zone. The organiser's reading is the
/// truth; the instant is derived from it with the stored zone, so the three cannot disagree.
/// </summary>
/// <remarks>
/// <para>
/// Use it for a time that is not bound to a place: an online briefing an organiser in Zurich sets for 10:00
/// is at 10:00 in <c>Europe/Zurich</c> and shown at 04:00 to a viewer in New York. A clock reading at a place
/// is a <see cref="PlaceTime"/>, or a <see cref="ZonedPlaceTime"/> when the place has its own zone.
/// </para>
/// <para>
/// The only way to get a value is <see cref="Create"/> or <see cref="TryCreate"/>, which resolve the id with
/// <see cref="ZonedTime.TryFindIanaTimeZone"/> and derive the instant with <see cref="ZonedTime.TryToInstant"/>:
/// a reading in a spring-forward gap has no instant, and a reading in a fall-back overlap denotes its earlier
/// occurrence. <see cref="Rederive"/> derives the instant again after an update of the time zone data, and
/// <see cref="WithTimeZone"/> keeps the reading in another zone.
/// </para>
/// <para>
/// Two values are equal when their readings, zone ids and instants are. EF Core maps a property of this type
/// as a complex type; <c>Csag.Blueprint.Infrastructure</c> names its columns <c>{Property}Local</c>,
/// <c>{Property}TimeZoneId</c> and <c>{Property}At</c>.
/// </para>
/// </remarks>
[ComplexType]
public sealed record ViewerTime
{
    private ViewerTime(DateTime local, string timeZoneId, DateTimeOffset at)
    {
        this.Local = local;
        this.TimeZoneId = timeZoneId;
        this.At = at;
    }

    /// <summary>
    /// Gets the clock reading the organiser set, with <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public DateTime Local { get; private init; }

    /// <summary>
    /// Gets the IANA id of the organiser's zone, for example <c>Europe/Zurich</c>.
    /// </summary>
    public string TimeZoneId { get; private init; }

    /// <summary>
    /// Gets the instant the reading denotes, in UTC.
    /// </summary>
    public DateTimeOffset At { get; private init; }

    /// <summary>
    /// Creates a viewer time from the clock reading an organiser set in the zone <paramref name="timeZoneId"/> names.
    /// </summary>
    /// <param name="local">The organiser's clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="timeZoneId">The IANA id of the organiser's zone.</param>
    /// <param name="viewerTime">The viewer time when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the id is an IANA id this host can resolve and the reading exists in its
    /// zone; <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryCreate(DateTime local, string? timeZoneId, [NotNullWhen(true)] out ViewerTime? viewerTime)
    {
        viewerTime = LocalReadings.TryDerive(local, timeZoneId, out var reading, out var at)
            ? new ViewerTime(reading, timeZoneId, at)
            : null;
        return viewerTime is not null;
    }

    /// <summary>
    /// Creates a viewer time from the clock reading an organiser set in the zone <paramref name="timeZoneId"/> names.
    /// </summary>
    /// <param name="local">The organiser's clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="timeZoneId">The IANA id of the organiser's zone.</param>
    /// <returns>The viewer time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The id is not an IANA id this host can resolve, or the reading falls in a spring-forward gap of its zone.
    /// </exception>
    public static ViewerTime Create(DateTime local, string timeZoneId)
    {
        var (reading, at) = LocalReadings.Derive(local, LocalReadings.FindZone(timeZoneId, nameof(timeZoneId)));
        return new ViewerTime(reading, timeZoneId, at);
    }

    /// <summary>
    /// Derives the instant again from the reading, with the current rules of the stored zone: after an update
    /// of the time zone data.
    /// </summary>
    /// <remarks>
    /// A reading that the rules now put into a spring-forward gap is moved forward by the length of the gap
    /// (02:30 in a gap from 02:00 to 03:00 becomes 03:30), and <c>Shifted</c> reports it, so the caller can
    /// tell the organiser.
    /// </remarks>
    /// <returns>The viewer time with the derived instant, and whether its reading had to be moved.</returns>
    /// <exception cref="TimeZoneNotFoundException">This host cannot resolve the stored zone id.</exception>
    /// <exception cref="InvalidOperationException">The reading has no instant in the zone even when moved.</exception>
    public (ViewerTime Value, bool Shifted) Rederive()
    {
        var (reading, at, shifted) = LocalReadings.DeriveShiftingGap(this.Local, LocalReadings.FindStoredZone(this.TimeZoneId));
        return (new ViewerTime(reading, this.TimeZoneId, at), shifted);
    }

    /// <summary>
    /// Changes the organiser's zone, keeping the reading and deriving its instant there.
    /// </summary>
    /// <remarks>
    /// A reading that falls in a spring-forward gap of the new zone is moved forward by the length of the gap,
    /// and <c>Shifted</c> reports it.
    /// </remarks>
    /// <param name="timeZoneId">The IANA id of the new zone.</param>
    /// <returns>The viewer time in the new zone, and whether its reading had to be moved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The id is not an IANA id this host can resolve.</exception>
    /// <exception cref="InvalidOperationException">The reading has no instant in the zone even when moved.</exception>
    public (ViewerTime Value, bool Shifted) WithTimeZone(string timeZoneId)
    {
        var (reading, at, shifted) = LocalReadings.DeriveShiftingGap(this.Local, LocalReadings.FindZone(timeZoneId, nameof(timeZoneId)));
        return (new ViewerTime(reading, timeZoneId, at), shifted);
    }
}
