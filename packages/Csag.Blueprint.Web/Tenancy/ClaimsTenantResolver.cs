namespace Csag.Blueprint.Web.Tenancy;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Microsoft.AspNetCore.Http;

/// <summary>
/// The package's default <see cref="ITenantResolver"/>: reads the active tenant from the
/// authenticated session's <c>TenantId</c> claim.
/// <para>
/// This is "session-resolved" addressing — the tenant is a property of who you are signed in as,
/// not of the URL you requested. It is why an application needs an in-app tenant switcher (an
/// endpoint that replaces the claim and recomputes authorization), and why nothing tenant-specific
/// can be rendered before sign-in.
/// </para>
/// </summary>
public sealed class ClaimsTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Anonymous requests have no tenant. This is the normal path for anonymous endpoints
        // (sign-in, onboarding links, health checks), so it must be quiet rather than exceptional.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult<Guid?>(null);
        }

        var tenantIdClaim = context.User.FindFirstValue(IdentityClaimTypes.TenantId);
        if (string.IsNullOrEmpty(tenantIdClaim)
            || !Guid.TryParse(tenantIdClaim, CultureInfo.InvariantCulture, out var tenantId))
        {
            // An authenticated user with no parseable tenant claim is a legitimate state: a platform-scope
            // administrator who belongs to no tenant, or a user whose memberships were all removed. They
            // get no tenant context, and the query filters fail closed.
            return ValueTask.FromResult<Guid?>(null);
        }

        return ValueTask.FromResult<Guid?>(tenantId);
    }
}
