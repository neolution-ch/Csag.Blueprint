namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Represents a service account that can authenticate via JWT tokens.
/// </summary>
public sealed class BlueprintServiceAccount : IMustHaveTenant, IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the service account.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier that owns this service account.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the friendly name of the service account.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID used for authentication.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hashed client secret (bcrypt).
    /// </summary>
    public string ClientSecretHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the roles assigned to this service account.
    /// Stored as a JSON array in the database via EF Core primitive collection mapping.
    /// </summary>
    public IList<string> Roles { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the explicit permissions assigned to this service account.
    /// These are added on top of any permissions derived from roles.
    /// Stored as a JSON array in the database via EF Core primitive collection mapping.
    /// </summary>
    public IList<string> Permissions { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether the service account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc/>
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? UpdatedAt { get; set; }
}
