namespace Csag.Blueprint.Domain.Time;

using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A place time: the clock reading at a place, together with the instant it denotes. The reading is the
/// truth; the instant is derived from it with the zone of the place, so the two cannot disagree.
/// </summary>
/// <remarks>
/// <para>
/// The zone is not stored. It is the zone of the place, which the caller knows: the tenant's zone
/// (<c>ITenantTimeZoneProvider</c>), or the zone of a place the entity belongs to. A place that stores its
/// own zone with each reading uses <see cref="ZonedPlaceTime"/>.
/// </para>
/// <para>
/// The only way to get a value is <see cref="Create"/> or <see cref="TryCreate"/>, which derive the instant
/// with <see cref="ZonedTime.TryToInstant"/>: a reading in a spring-forward gap has no instant, and a reading
/// in a fall-back overlap denotes its earlier occurrence. When the rules of the zone change (new time zone
/// data) or the place moves to another zone, the stored instant is out of date; <see cref="Rederive"/>
/// keeps the reading and derives the instant again.
/// </para>
/// <para>
/// Two values are equal when their readings and instants are. EF Core maps a property of this type as a
/// complex type; <c>Csag.Blueprint.Infrastructure</c> names its columns <c>{Property}Local</c> and
/// <c>{Property}At</c>.
/// </para>
/// </remarks>
[ComplexType]
public sealed record PlaceTime
{
    private PlaceTime(DateTime local, DateTimeOffset at)
    {
        this.Local = local;
        this.At = at;
    }

    /// <summary>
    /// Gets the clock reading at the place, with <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public DateTime Local { get; private init; }

    /// <summary>
    /// Gets the instant the reading denotes, in UTC.
    /// </summary>
    public DateTimeOffset At { get; private init; }

    /// <summary>
    /// Creates a place time from a clock reading at a place in <paramref name="zone"/>.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <param name="placeTime">The place time when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the reading exists in the zone; <see langword="false"/> when it falls in a
    /// spring-forward gap.
    /// </returns>
    public static bool TryCreate(DateTime local, TimeZoneInfo zone, [NotNullWhen(true)] out PlaceTime? placeTime)
    {
        placeTime = LocalReadings.TryDerive(local, zone, out var reading, out var at) ? new PlaceTime(reading, at) : null;
        return placeTime is not null;
    }

    /// <summary>
    /// Creates a place time from a clock reading at a place in <paramref name="zone"/>.
    /// </summary>
    /// <param name="local">The clock reading. Its <see cref="DateTime.Kind"/> is ignored.</param>
    /// <param name="zone">The zone of the place.</param>
    /// <returns>The place time.</returns>
    /// <exception cref="ArgumentException">The reading falls in a spring-forward gap of the zone.</exception>
    public static PlaceTime Create(DateTime local, TimeZoneInfo zone)
    {
        var (reading, at) = LocalReadings.Derive(local, zone);
        return new PlaceTime(reading, at);
    }

    /// <summary>
    /// Derives the instant again from the reading, with the current rules of <paramref name="zone"/>: after
    /// an update of the time zone data, or when the place now has another zone.
    /// </summary>
    /// <remarks>
    /// A reading that the rules now put into a spring-forward gap is moved forward by the length of the gap
    /// (02:30 in a gap from 02:00 to 03:00 becomes 03:30), and <c>Shifted</c> reports it, so the caller can
    /// tell whoever relies on the reading.
    /// </remarks>
    /// <param name="zone">The zone of the place.</param>
    /// <returns>The place time with the derived instant, and whether its reading had to be moved.</returns>
    /// <exception cref="InvalidOperationException">The reading has no instant in the zone even when moved.</exception>
    public (PlaceTime Value, bool Shifted) Rederive(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var (reading, at, shifted) = LocalReadings.DeriveShiftingGap(this.Local, zone);
        return (new PlaceTime(reading, at), shifted);
    }
}
