namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Blueprint-owned canonical tenant membership entity representing a many-to-many
/// relationship between users and tenants. Generic on both user and tenant types
/// so that the application can derive concrete types and EF navigations are fully typed.
/// </summary>
/// <typeparam name="TUser">The concrete user type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
public class BlueprintTenantMembership<TUser, TTenant> : IAuditable
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the date when the user joined this tenant.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this membership was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this membership was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user associated with this membership.
    /// </summary>
    public TUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tenant associated with this membership.
    /// </summary>
    public TTenant Tenant { get; set; } = null!;
}
