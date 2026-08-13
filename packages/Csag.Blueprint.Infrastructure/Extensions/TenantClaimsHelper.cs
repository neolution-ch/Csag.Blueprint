namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;

/// <summary>
/// Helper for writing the tenant-scope claim onto an identity from a single source, so the tenant claim
/// is composed identically across sign-in, tenant switch, and session refresh.
/// </summary>
public static class TenantClaimsHelper
{
    /// <summary>
    /// Replaces the tenant-scope claim on the given identity with the specified tenant. Any existing
    /// <see cref="IdentityClaimTypes.TenantId"/> claims are removed first so the identity always carries
    /// exactly one tenant claim.
    /// </summary>
    /// <param name="identity">The claims identity to update.</param>
    /// <param name="tenantId">The tenant the identity should be scoped to.</param>
    public static void SetTenantClaim(this ClaimsIdentity identity, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var existingTenantClaims = identity.FindAll(IdentityClaimTypes.TenantId).ToList();
        foreach (var claim in existingTenantClaims)
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(IdentityClaimTypes.TenantId, tenantId.ToString()));
    }
}
