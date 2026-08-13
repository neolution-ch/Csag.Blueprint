namespace Csag.Blueprint.Infrastructure.Authentication;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Default implementation of <see cref="IAuthenticatedPrincipalBuilder{TUser}"/>.
/// Composes the existing user-profile and tenant claim helpers with the shared
/// <see cref="ITenantAuthorizationResolver"/> — the single source of truth for combining the user's
/// platform-scope global roles, tenant-scoped roles, role-derived permissions, and direct tenant-scoped
/// permission grants — then applies <see cref="IClaimsTransformation"/> so the resulting principal
/// matches what an authenticated request would observe after the transformation pipeline runs.
/// <para>
/// The resolver's full permission set is baked into the identity. This is safe alongside the claims
/// transformation because the transformation only adds permission claims that are missing.
/// </para>
/// </summary>
/// <typeparam name="TUser">The application user type managed by Identity.</typeparam>
public sealed class AuthenticatedPrincipalBuilder<TUser> : IAuthenticatedPrincipalBuilder<TUser>
    where TUser : class, IUserProfileClaimsSource
{
    private readonly UserManager<TUser> userManager;
    private readonly ITenantAuthorizationResolver tenantAuthorizationResolver;
    private readonly IClaimsTransformation claimsTransformation;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedPrincipalBuilder{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">Identity user manager used to load global (platform) roles.</param>
    /// <param name="tenantAuthorizationResolver">The shared resolver that composes the effective roles and permissions for the active tenant.</param>
    /// <param name="claimsTransformation">The application's <see cref="IClaimsTransformation"/> (typically the permission-claims transformation).</param>
    public AuthenticatedPrincipalBuilder(
        UserManager<TUser> userManager,
        ITenantAuthorizationResolver tenantAuthorizationResolver,
        IClaimsTransformation claimsTransformation)
    {
        this.userManager = userManager;
        this.tenantAuthorizationResolver = tenantAuthorizationResolver;
        this.claimsTransformation = claimsTransformation;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> BuildAsync(TUser user, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.SetUserProfileClaims(user);

        if (tenantId.HasValue)
        {
            identity.SetTenantClaim(tenantId.Value);
        }

        // The resolver owns the composition rule: only platform-scope roles are honored from the user's
        // global role set (operational roles are strictly tenant-scoped and can never leak into a tenant),
        // tenant-scoped roles and direct grants apply when a tenant is active, and the effective permission
        // set is the union of role-derived permissions and direct grants.
        var globalRoles = await this.userManager.GetRolesAsync(user);
        var (roles, permissions) = await this.tenantAuthorizationResolver.ResolveAsync(user.Id, globalRoles, tenantId, cancellationToken);

        identity.SetAuthorizationClaims(roles.ToList(), permissions.ToList());

        var principal = new ClaimsPrincipal(identity);
        return await this.claimsTransformation.TransformAsync(principal);
    }
}
