namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Tenant-scoped direct permission grant. Associates an individual permission with a user inside the
/// context of a single tenant, independently of that user's roles. This is the permission analog of
/// <see cref="BlueprintTenantMembershipRole{TUser, TTenant}"/>: it lets an administrator grant a user
/// extra capability within one tenant without affecting the user's permissions in any other tenant.
/// <para>
/// Unlike roles (which reference the shared <c>AspNetRoles</c> catalog by identifier), permissions are
/// free-form string values defined in application code, so the permission text itself is part of the
/// composite primary key.
/// </para>
/// </summary>
/// <typeparam name="TUser">The concrete user type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - 'Permission' is the domain concept this entity represents
public class BlueprintTenantMembershipPermission<TUser, TTenant> : IAuditable
#pragma warning restore CA1711
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
    /// Gets or sets the permission value granted directly to the user within the tenant
    /// (for example <c>"orders:delete"</c>). Part of the composite primary key.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

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
    /// Gets or sets the membership this permission grant belongs to.
    /// </summary>
    public BlueprintTenantMembership<TUser, TTenant> Membership { get; set; } = null!;
}
