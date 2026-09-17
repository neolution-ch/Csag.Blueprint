namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that have an active date range.
/// Entities implementing this interface are considered active only within
/// the specified date range between ActiveFrom and ActiveUntil.
/// </summary>
public interface IHasActiveRange
{
    /// <summary>
    /// Gets or sets the timestamp from which this entity becomes active.
    /// <para>
    /// When null, the entity is considered active from the beginning of time.
    /// When set, the entity is only active on or after this timestamp.
    /// </para>
    /// </summary>
    DateTimeOffset? ActiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the timestamp until which this entity remains active.
    /// <para>
    /// When null, the entity is considered active until the end of time.
    /// When set, the entity is only active before this timestamp (exclusive).
    /// </para>
    /// </summary>
    DateTimeOffset? ActiveUntil { get; set; }
}
