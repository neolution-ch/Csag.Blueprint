namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface indicating that an entity participates in audit logging.
/// All domain entities should implement this interface unless explicitly exempt.
/// Entities that do not implement this interface will not have their changes tracked by the audit system.
/// <para>
/// <see cref="CreatedAt"/>, <see cref="UpdatedAt"/>, <see cref="CreatedByActor"/> and <see cref="UpdatedByActor"/>
/// are automatically managed by the AuditableTimestampInterceptor — do not set them manually in
/// endpoint or service code.
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

    /// <summary>
    /// Gets or sets the actor that created the entity — a readable, point-in-time snapshot label,
    /// NOT a foreign key or a current-identity reference. Holds the user's email for authenticated
    /// users, the <c>sa-{clientId}</c> client id for service accounts, and null when there is no
    /// acting user (data seeding, background services, migrations, system operations).
    /// Automatically set on insert by the AuditableTimestampInterceptor.
    /// <para>
    /// Because it is a snapshot, it records who acted <em>at the time</em> and may not resolve to a
    /// current user after an email change or reassignment. To resolve the canonical/current identity,
    /// or to join actor-to-actor across tables, use the id-keyed audit log — that is the source of truth.
    /// </para>
    /// </summary>
    string? CreatedByActor { get; set; }

    /// <summary>
    /// Gets or sets the actor that last updated the entity — a point-in-time snapshot label with the
    /// same semantics as <see cref="CreatedByActor"/> (email for users, <c>sa-{clientId}</c> for
    /// service accounts). Left null until the row is first modified, and null when there is no acting
    /// user. Automatically set on update by the AuditableTimestampInterceptor.
    /// </summary>
    string? UpdatedByActor { get; set; }
}
