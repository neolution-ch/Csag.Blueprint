namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that support soft deletion.
/// When an entity is soft deleted, it remains in the database but is marked as deleted
/// with a timestamp instead of being physically removed.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets or sets the timestamp when the entity was marked as deleted.
    /// <para>
    /// When null, the entity is considered active (not deleted).
    /// When set to a value, the entity is considered soft deleted as of that timestamp.
    /// </para>
    /// </summary>
    DateTimeOffset? DeletedAt { get; set; }
}
