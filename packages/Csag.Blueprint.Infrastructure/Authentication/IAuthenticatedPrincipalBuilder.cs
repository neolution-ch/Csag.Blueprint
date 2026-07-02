namespace Csag.Blueprint.Infrastructure.Authentication;

using System.Security.Claims;
using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Builds an authenticated <see cref="ClaimsPrincipal"/> for sign-in flows
/// (password login, external OAuth callback, future SSO paths, ...).
///
/// IMPORTANT: All sign-in flows should call <see cref="BuildAsync"/>. Because
/// the principal is constructed manually rather than read from a cookie, the
/// <see cref="Microsoft.AspNetCore.Authentication.IClaimsTransformation"/>
/// pipeline does not run automatically. Without the transformation step that
/// the implementation applies, role-derived permission claims would be
/// missing from the session ticket and the current request context
/// immediately after sign-in.
/// </summary>
/// <typeparam name="TUser">The application user type managed by Identity.</typeparam>
public interface IAuthenticatedPrincipalBuilder<TUser>
    where TUser : class, IUserProfileClaimsSource
{
    /// <summary>
    /// Builds a transformed <see cref="ClaimsPrincipal"/> ready to be passed to <c>HttpContext.SignInAsync</c>.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="tenantId">The current tenant id to embed as a claim, or <c>null</c> for flows that do not (yet) resolve a tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A principal with profile, tenant, role, permission and any transformation-derived claims applied.</returns>
    Task<ClaimsPrincipal> BuildAsync(TUser user, Guid? tenantId, CancellationToken cancellationToken = default);
}
