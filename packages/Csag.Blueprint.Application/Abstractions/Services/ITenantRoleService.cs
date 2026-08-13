namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Manages tenant-scoped role assignments — the tenant-aware analog of ASP.NET Identity's
/// per-user role assignment. Roles are drawn from the shared, global role catalog
/// (<c>AspNetRoles</c>) but are granted to a user only within a specific tenant.
/// <para>
/// The contract is expressed purely in terms of identifiers and role names (not tenant entity
/// types) so that callers which are generic over the user type only — such as the authenticated
/// principal builder — can depend on it.
/// </para>
/// </summary>
public interface ITenantRoleService
{
    /// <summary>
    /// Gets the role names assigned to a user within a specific tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The role names granted to the user inside the tenant; empty if none.</returns>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Sets the complete set of role assignments for a user within a tenant. The user must already
    /// be a member of the tenant. Existing assignments not present in <paramref name="roleNames"/>
    /// are removed and missing assignments are added.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="roleNames">The desired set of role names (from the shared role catalog).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the assignments have been persisted.</returns>
    /// <exception cref="InvalidTenantAssignmentException">Thrown when a requested role name does not exist in the catalog,
    /// or is a platform-scope role that must never be assigned within a tenant.</exception>
    /// <exception cref="TenantMembershipRequiredException">Thrown when the user is not a member of the tenant
    /// (a skipped precondition or a concurrent membership removal).</exception>
    Task SetRolesAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> roleNames, CancellationToken ct = default);
}
