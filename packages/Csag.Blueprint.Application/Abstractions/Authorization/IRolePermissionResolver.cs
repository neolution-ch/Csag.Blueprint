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
}
