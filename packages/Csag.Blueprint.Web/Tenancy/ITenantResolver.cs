namespace Csag.Blueprint.Web.Tenancy;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Determines which tenant an incoming request belongs to — the <b>addressing</b> seam of the
/// multi-tenancy model.
/// <para>
/// This package ships <see cref="ClaimsTenantResolver"/> as the default, which reads the tenant from
/// the authenticated session's <c>TenantId</c> claim. That is a deliberate default, not the only
/// option: other generic addressing strategies — vanity subdomains (<c>acme.example.com</c>),
/// path-scoped tenants (<c>/t/acme</c>), a header-driven tenant — belong in the Blueprint packages as
/// additional implementations. A consuming application should only write its own resolver when its
/// addressing scheme is genuinely app-specific and no packaged one fits.
/// </para>
/// <para>
/// <b>Registration.</b> The default is registered with <c>TryAddScoped</c>, so a resolver registered
/// before <c>AddBlueprintServices</c> wins — the package default never has to be removed.
/// </para>
/// <para>
/// <b>Two things to know before replacing it.</b> First, <c>TenantMiddleware</c> currently runs
/// <i>after</i> <c>UseAuthentication</c>/<c>UseAuthorization</c>, because the default resolver needs
/// the authenticated principal. A resolver that reads the host or path does not, and moving the
/// middleware earlier is what enables per-tenant branding and per-tenant identity-provider routing on
/// the sign-in page. Second, resolving the tenant before authentication means sign-in itself no longer
/// derives the tenant from the ticket, so session composition needs revisiting too — the resolver is
/// the seam, not the whole job.
/// </para>
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves the tenant for the current request, or <see langword="null"/> when the request has no
    /// tenant context. Returning <see langword="null"/> is normal and must not throw: unauthenticated
    /// requests, platform-scope endpoints, and users who belong to no tenant all land here, and the
    /// tenant query filters deliberately fail closed in that state.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved tenant identifier, or <see langword="null"/> when there is none.</returns>
    ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
