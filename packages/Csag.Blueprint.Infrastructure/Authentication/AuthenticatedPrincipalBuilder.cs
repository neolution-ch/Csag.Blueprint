namespace Csag.Blueprint.Infrastructure.Authentication;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Default implementation of <see cref="IAuthenticatedPrincipalBuilder{TUser}"/>.
/// Composes the existing user-profile, tenant and authorization claim helpers,
/// then applies <see cref="IClaimsTransformation"/> so the resulting principal
/// matches what an authenticated request would observe after the transformation
/// pipeline runs.
/// </summary>
/// <typeparam name="TUser">The application user type managed by Identity.</typeparam>
public sealed class AuthenticatedPrincipalBuilder<TUser> : IAuthenticatedPrincipalBuilder<TUser>
    where TUser : class, IUserProfileClaimsSource
{
    private readonly UserManager<TUser> userManager;
    private readonly IClaimsTransformation claimsTransformation;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedPrincipalBuilder{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">Identity user manager used to load roles and permission claims.</param>
    /// <param name="claimsTransformation">The application's <see cref="IClaimsTransformation"/> (typically the permission-claims transformation).</param>
    public AuthenticatedPrincipalBuilder(UserManager<TUser> userManager, IClaimsTransformation claimsTransformation)
    {
        this.userManager = userManager;
        this.claimsTransformation = claimsTransformation;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> BuildAsync(TUser user, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.SetUserProfileClaims(user);

        if (tenantId.HasValue)
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        var (roles, permissions) = await this.userManager.GetUserRolesAndPermissionsAsync(user, cancellationToken);
        identity.SetAuthorizationClaims(roles, permissions);

        var principal = new ClaimsPrincipal(identity);
        return await this.claimsTransformation.TransformAsync(principal);
    }
}
