namespace Csag.Blueprint.Domain.Time;

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A clock reading together with the IANA id of the zone it was read in and the instant it denotes. The
/// reading is the truth; the instant is derived from it with the stored zone, so the three cannot disagree.
/// </summary>
/// <remarks>
/// <para>
/// Use it where the zone travels with each reading instead of being known from elsewhere. That covers two
/// kinds of time:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A place time at a place with its own zone: a stand at a boat show in Miami, for a tenant in Zurich, opens at
/// 10:00 in <c>America/New_York</c>. It is shown as the clock time at the place, labelled with its zone,
/// whatever zone the viewer is in.
/// </description></item>
/// <item><description>
/// A viewer time an organiser sets in their own zone: an online briefing at 10:00 in <c>Europe/Zurich</c>. Each
/// viewer sees the instant in their own zone, 04:00 in New York.
/// </description></item>
/// </list>
/// <para>
/// The value is the same for both; which display rule applies is a property of the entity property and its DTO,
/// not of this type. A <see cref="PlaceTime"/> holds no zone: its zone is the tenant's, or that of a place the
/// entity belongs to, and the caller passes it in.
/// </para>
/// <para>
/// The only way to get a value is <see cref="Create"/> or <see cref="TryCreate"/>, which resolve the id with
/// <see cref="TimeZones.TryFindIanaTimeZone"/> and derive the instant with <see cref="TimeZones.TryToInstant"/>:
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
public sealed record ZonedTime
{
    private ZonedTime(DateTime local, string timeZoneId, DateTimeOffset at)
    {
        this.Local = local;
        this.TimeZoneId = timeZoneId;
        this.At = at;
    }

    /// <summary>
    /// Gets the clock reading in the zone, with <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public DateTime Local { get; private init; }

    /// <summary>
    /// Gets the IANA id of the zone the reading was read in, for example <c>America/New_York</c>.
    /// </summary>
    public string TimeZoneId { get; private init; }

    /// <summary>
    /// Gets the instant the reading denotes, in UTC.
    /// </summary>
    public DateTimeOffset At { get; private init; }

    /// <summary>
    /// Creates a zoned time from a clock reading in the zone <paramref name="timeZoneId"/> names.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="timeZoneId">The IANA id of the zone the reading was read in.</param>
    /// <param name="zonedTime">The zoned time when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the id is an IANA id this host can resolve and the reading exists in its
    /// zone; <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryCreate(DateTime local, string? timeZoneId, [NotNullWhen(true)] out ZonedTime? zonedTime)
    {
        zonedTime = LocalReadings.TryDerive(local, timeZoneId, out var reading, out var at)
            ? new ZonedTime(reading, timeZoneId, at)
            : null;
        return zonedTime is not null;
    }

    /// <summary>
    /// Creates a zoned time from a clock reading in the zone <paramref name="timeZoneId"/> names.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="timeZoneId">The IANA id of the zone the reading was read in.</param>
    /// <returns>The zoned time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The id is not an IANA id this host can resolve, or the reading falls in a spring-forward gap of its zone.
    /// </exception>
    public static ZonedTime Create(DateTime local, string timeZoneId)
    {
        var (reading, at) = LocalReadings.Derive(local, LocalReadings.FindZone(timeZoneId, nameof(timeZoneId)));
        return new ZonedTime(reading, timeZoneId, at);
    }

    /// <summary>
    /// Derives the instant again from the reading, with the current rules of the stored zone: after an update
    /// of the time zone data.
    /// </summary>
    /// <remarks>
    /// A reading that the rules now put into a spring-forward gap is moved forward by the length of the gap
    /// (02:30 in a gap from 02:00 to 03:00 becomes 03:30), and <c>Shifted</c> reports it, so the caller can
    /// tell whoever relies on the reading.
    /// </remarks>
    /// <returns>The zoned time with the derived instant, and whether its reading had to be moved.</returns>
    /// <exception cref="TimeZoneNotFoundException">This host cannot resolve the stored zone id.</exception>
    /// <exception cref="InvalidOperationException">The reading has no instant in the zone even when moved.</exception>
    public (ZonedTime Value, bool Shifted) Rederive()
    {
        var (reading, at, shifted) = LocalReadings.DeriveShiftingGap(this.Local, LocalReadings.FindStoredZone(this.TimeZoneId));
        return (new ZonedTime(reading, this.TimeZoneId, at), shifted);
    }

    /// <summary>
    /// Changes the zone, keeping the reading and deriving its instant there: the place is now in another zone,
    /// or the organiser sets the time in another zone.
    /// </summary>
    /// <remarks>
    /// A reading that falls in a spring-forward gap of the new zone is moved forward by the length of the gap,
    /// and <c>Shifted</c> reports it.
    /// </remarks>
    /// <param name="timeZoneId">The IANA id of the new zone.</param>
    /// <returns>The zoned time in the new zone, and whether its reading had to be moved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The id is not an IANA id this host can resolve.</exception>
    /// <exception cref="InvalidOperationException">The reading has no instant in the zone even when moved.</exception>
    public (ZonedTime Value, bool Shifted) WithTimeZone(string timeZoneId)
    {
        var (reading, at, shifted) = LocalReadings.DeriveShiftingGap(this.Local, LocalReadings.FindZone(timeZoneId, nameof(timeZoneId)));
        return (new ZonedTime(reading, timeZoneId, at), shifted);
    }
}
