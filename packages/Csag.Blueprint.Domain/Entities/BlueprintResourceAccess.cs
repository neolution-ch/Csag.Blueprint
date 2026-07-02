namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Represents fine-grained resource-level permissions for users or service accounts.
/// </summary>
public sealed class BlueprintResourceAccess : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for this permission entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the type of entity this permission applies to.
    /// Valid values: "User" or "ServiceAccount"
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the entity (UserId or ServiceAccountId).
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Gets or sets the type of resource this permission applies to.
    /// Examples: "Pedalo", "Reservation", "MaintenanceRecord"
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific resource ID this permission applies to.
    /// NULL means permission applies to all resources of this type.
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the permission level.
    /// Valid values: "Read", "Write", "Delete"
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when this permission was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this permission was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
