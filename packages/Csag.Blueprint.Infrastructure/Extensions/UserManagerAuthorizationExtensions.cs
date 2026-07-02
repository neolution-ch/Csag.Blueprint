namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Application.Claims;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Extension methods for <see cref="UserManager{TUser}"/> to query authorization data.
/// </summary>
public static class UserManagerAuthorizationExtensions
{
    /// <summary>
    /// Gets user roles and permission claims from the identity store.
    /// Prefer this overload when the user instance is already loaded; it avoids
    /// a redundant <see cref="UserManager{TUser}.FindByIdAsync"/> round-trip.
    /// </summary>
    /// <typeparam name="TUser">The application user type managed by Identity.</typeparam>
    /// <param name="userManager">The user manager instance.</param>
    /// <param name="user">The user to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of role and permission lists.</returns>
    public static async Task<(List<string> Roles, List<string> Permissions)> GetUserRolesAndPermissionsAsync<TUser>(
        this UserManager<TUser> userManager,
        TUser user,
        CancellationToken cancellationToken = default)
        where TUser : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var claims = await userManager.GetClaimsAsync(user);
        var permissions = claims
            .Where(c => c.Type == IdentityClaimTypes.Permission)
            .Select(c => c.Value)
            .ToList();

        return (roles, permissions);
    }

    /// <summary>
    /// Gets user roles and permission claims from the identity store by user id.
    /// Loads the user first; prefer the overload that accepts a <typeparamref name="TUser"/> instance when one is already available.
    /// </summary>
    /// <typeparam name="TUser">The application user type managed by Identity.</typeparam>
    /// <param name="userManager">The user manager instance.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of role and permission lists; empty if no user exists for the given id.</returns>
    public static async Task<(List<string> Roles, List<string> Permissions)> GetUserRolesAndPermissionsAsync<TUser>(
        this UserManager<TUser> userManager,
        Guid userId,
        CancellationToken cancellationToken = default)
        where TUser : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (new List<string>(), new List<string>());
        }

        return await userManager.GetUserRolesAndPermissionsAsync(user, cancellationToken);
    }
}
