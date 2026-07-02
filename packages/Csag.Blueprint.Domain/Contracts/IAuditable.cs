namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface indicating that an entity participates in audit logging.
/// All domain entities should implement this interface unless explicitly exempt.
/// Entities that do not implement this interface will not have their changes tracked by the audit system.
/// <para>
/// <see cref="CreatedAt"/> and <see cref="UpdatedAt"/> are automatically managed by the
/// AuditableTimestampInterceptor — do not set them manually in endpoint or service code.
/// </para>
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets or sets the timestamp when the entity was created.
    /// Automatically set to <see cref="DateTimeOffset.UtcNow"/> on insert.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the entity was last updated.
    /// Automatically set to <see cref="DateTimeOffset.UtcNow"/> on update.
    /// </summary>
    DateTimeOffset? UpdatedAt { get; set; }
}
