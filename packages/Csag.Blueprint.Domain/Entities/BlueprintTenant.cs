namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Blueprint-owned canonical tenant entity. Applications extend this class to add
/// domain-specific tenant properties (e.g., timezone, branding, subscription tier).
/// </summary>
public class BlueprintTenant : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the tenant.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the tenant.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the tenant was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the tenant was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
