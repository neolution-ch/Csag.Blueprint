namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Manages tenant-scoped direct permission grants — the permission analog of
/// <see cref="ITenantRoleService"/>. A direct permission grants a user an individual capability inside
/// a single tenant, independently of (and in addition to) the permissions derived from their roles.
/// <para>
/// The contract is expressed purely in terms of identifiers and permission strings (not tenant entity
/// types) so that callers which are generic over the user type only — such as the authenticated
/// principal builder and the session manager — can depend on it.
/// </para>
/// </summary>
public interface ITenantPermissionService
{
    /// <summary>
    /// Gets the permissions granted directly to a user within a specific tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The permissions granted directly to the user inside the tenant; empty if none.</returns>
    Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Sets the complete set of direct permission grants for a user within a tenant. The user must
    /// already be a member of the tenant. Existing grants not present in <paramref name="permissions"/>
    /// are removed and missing grants are added.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="permissions">The desired set of permission values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the grants have been persisted.</returns>
    /// <exception cref="InvalidTenantAssignmentException">Thrown when a requested permission is not tenant-grantable
    /// (unknown, or platform-scope and therefore never grantable within a tenant).</exception>
    /// <exception cref="TenantMembershipRequiredException">Thrown when the user is not a member of the tenant
    /// (a skipped precondition or a concurrent membership removal).</exception>
    Task SetPermissionsAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> permissions, CancellationToken ct = default);
}
