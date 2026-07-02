namespace Csag.Blueprint.Infrastructure.Authorization;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Claims;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Transforms user claims by adding permission claims based on their roles.
/// This runs after authentication and before authorization, ensuring users have the correct
/// permission claims for their role(s) on every request.
/// </summary>
public sealed class PermissionClaimsTransformation : IClaimsTransformation
{
    private readonly IRolePermissionResolver permissionResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionClaimsTransformation"/> class.
    /// </summary>
    /// <param name="permissionResolver">The resolver for role-to-permission mapping.</param>
    public PermissionClaimsTransformation(IRolePermissionResolver permissionResolver)
    {
        this.permissionResolver = permissionResolver;
    }

    /// <summary>
    /// Transforms the claims principal by adding permission claims based on roles.
    /// </summary>
    /// <param name="principal">The authenticated user's claims principal.</param>
    /// <returns>The transformed claims principal with permission claims added.</returns>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only transform if user is authenticated
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        var identity = (ClaimsIdentity)principal.Identity;

        // Get all roles the user has (materialize to avoid collection modified exception)
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        // Add permissions for each role
        foreach (var role in roles)
        {
            var permissions = this.permissionResolver.GetPermissionsForRole(role);

            foreach (var permission in permissions)
            {
                // Only add if not already present (prevents duplicates if user has multiple roles)
                if (!identity.HasClaim(IdentityClaimTypes.Permission, permission))
                {
                    identity.AddClaim(new Claim(IdentityClaimTypes.Permission, permission));
                }
            }
        }

        return Task.FromResult(principal);
    }
}
