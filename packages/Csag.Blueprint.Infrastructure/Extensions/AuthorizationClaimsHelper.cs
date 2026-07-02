namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;

/// <summary>
/// Helper class for managing authorization-related claims (roles and permissions).
/// </summary>
public static class AuthorizationClaimsHelper
{
    /// <summary>
    /// Adds or refreshes role and permission claims in the given identity.
    /// Removes existing role and permission claims and replaces them with the provided values.
    /// </summary>
    /// <param name="identity">The claims identity to update.</param>
    /// <param name="roles">The list of roles to add.</param>
    /// <param name="permissions">The list of permission claims to add.</param>
    public static void SetAuthorizationClaims(this ClaimsIdentity identity, IList<string> roles, IList<string> permissions)
    {
        ReplaceRoles(identity, roles);
        ReplacePermissions(identity, permissions);
    }

    private static void ReplacePermissions(ClaimsIdentity identity, IList<string> permissions)
    {
        var existingPermissionClaims = identity.FindAll(IdentityClaimTypes.Permission).ToList();
        foreach (var claim in existingPermissionClaims)
        {
            identity.RemoveClaim(claim);
        }

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.Permission, permission));
        }
    }

    private static void ReplaceRoles(ClaimsIdentity identity, IList<string> roles)
    {
        var existingRoleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        foreach (var claim in existingRoleClaims)
        {
            identity.RemoveClaim(claim);
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
}
