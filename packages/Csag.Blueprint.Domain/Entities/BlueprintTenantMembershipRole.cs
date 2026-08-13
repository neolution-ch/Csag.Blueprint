namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Tenant-scoped analog of ASP.NET Identity's <c>AspNetUserRoles</c>. Associates a role
/// (from the shared, global role catalog in <c>AspNetRoles</c>) with a user inside the
/// context of a single tenant. The same user can therefore hold different roles in
/// different tenants without affecting any other tenant.
/// </summary>
/// <typeparam name="TUser">The concrete user type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
public class BlueprintTenantMembershipRole<TUser, TTenant> : IAuditable
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <summary>
    /// Gets or sets the user identifier. Part of the composite primary key and of the
    /// foreign key back to the owning <see cref="BlueprintTenantMembership{TUser, TTenant}"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier. Part of the composite primary key and of the
    /// foreign key back to the owning <see cref="BlueprintTenantMembership{TUser, TTenant}"/>.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier referencing the shared role catalog (<c>AspNetRoles</c>).
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this assignment was created.
    /// Automatically set by the audit timestamp interceptor on insert.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this assignment was last updated.
    /// Automatically set by the audit timestamp interceptor on update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <inheritdoc/>
    public string? CreatedByActor { get; set; }

    /// <inheritdoc/>
    public string? UpdatedByActor { get; set; }

    /// <summary>
    /// Gets or sets the membership this role assignment belongs to.
    /// </summary>
    public BlueprintTenantMembership<TUser, TTenant> Membership { get; set; } = null!;
}
