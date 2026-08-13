namespace Csag.Blueprint.Application.Abstractions.Authorization;

using System.Collections.Generic;

/// <summary>
/// Resolves permissions for a given role.
/// Implementations provide role-to-permission mapping specific to the application's authorization model.
/// </summary>
public interface IRolePermissionResolver
{
    /// <summary>
    /// Gets all permissions associated with the specified role.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <returns>A collection of permission strings for the role. Returns an empty collection if the role is unknown.</returns>
    IEnumerable<string> GetPermissionsForRole(string role);

    /// <summary>
    /// Determines whether the specified role is a platform-scope role — one that is assigned globally
    /// (ASP.NET Identity user roles) rather than per tenant, and is therefore the only kind of role that may
    /// be honored from a user's global role set when composing their effective authorization for a tenant.
    /// Operational (tenant-scoped) roles must return <c>false</c> so that a global operational-role assignment
    /// can never leak into a tenant. See the effective-role composition in the principal builder, session
    /// refresh, and tenant-authorization resolver.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <returns><c>true</c> if the role is platform-scope; otherwise <c>false</c> (including for unknown roles).</returns>
    bool IsPlatformScopeRole(string role);

    /// <summary>
    /// Determines whether the specified permission may be granted directly to a user within a tenant.
    /// Platform-scope permissions — conferred only by platform-scope roles — and unknown permissions must
    /// return <c>false</c>, so that tenant user management can never persist a direct grant that would
    /// confer a platform capability at authentication time. This is the permission-side counterpart of
    /// <see cref="IsPlatformScopeRole"/>.
    /// </summary>
    /// <param name="permission">The permission string.</param>
    /// <returns><c>true</c> if the permission may be granted directly within a tenant; otherwise <c>false</c>.</returns>
    bool IsTenantGrantablePermission(string permission);
}
