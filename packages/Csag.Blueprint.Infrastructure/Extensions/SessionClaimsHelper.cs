namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Single-source claim composition for a session ticket's identity. Every place that (re)builds a
/// session's claims for a given tenant — session refresh and tenant switch — must apply the SAME set of
/// claims in the SAME way, or the paths silently drift (e.g. one path forgetting to refresh profile
/// claims). Both call this helper with authorization already resolved for the tenant, so callers keep
/// their own resolve strategy (refresh resolves once per distinct tenant and reuses it across that
/// tenant's sessions; switch resolves once for the single target tenant) and their own ticket storage
/// and commit orchestration.
/// </summary>
public static class SessionClaimsHelper
{
    /// <summary>
    /// Applies the tenant, profile, and authorization claims for a session scoped to
    /// <paramref name="tenantId"/>. The tenant claim is written only when a tenant is active (a
    /// tenant-less session carries none); profile and authorization claims are always (re)written.
    /// </summary>
    /// <param name="identity">The session ticket's identity to update in place.</param>
    /// <param name="user">The account whose profile claims are written.</param>
    /// <param name="tenantId">The session's active tenant, or <c>null</c> for a tenant-less session.</param>
    /// <param name="roles">The effective roles already resolved for the tenant.</param>
    /// <param name="permissions">The effective permissions already resolved for the tenant.</param>
    public static void ApplySessionClaims(
        this ClaimsIdentity identity,
        IUserProfileClaimsSource user,
        Guid? tenantId,
        IList<string> roles,
        IList<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (tenantId.HasValue)
        {
            identity.SetTenantClaim(tenantId.Value);
        }

        identity.SetUserProfileClaims(user);
        identity.SetAuthorizationClaims(roles, permissions);
    }
}
