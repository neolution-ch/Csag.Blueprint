namespace Csag.Blueprint.Web.Middleware;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that establishes the ambient tenant context for the duration of a request, so that EF
/// Core query filters and save interceptors know which tenant they are operating on.
/// <para>
/// <b>How the tenant is determined is not decided here.</b> That is delegated to
/// <see cref="ITenantResolver"/> — the addressing seam. The package default reads the session's
/// tenant claim (<see cref="ClaimsTenantResolver"/>); other addressing strategies (subdomain, path
/// segment, header) are further resolver implementations, and one registered before the default wins.
/// </para>
/// <para>
/// The context is always cleared afterwards, including on failure, because it is
/// <c>AsyncLocal</c>-backed and a pooled thread must never inherit the previous request's tenant.
/// </para>
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public TenantMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// Resolves the tenant for this request and publishes it to <see cref="TenantContext"/>.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="tenantResolver">
    /// The tenant resolver. Injected per-invocation rather than through the constructor so that a
    /// replacement implementation may be <b>scoped</b> — a resolver that maps a hostname to a tenant
    /// needs a database, and middleware constructors can only take singletons.
    /// </param>
    /// <returns>A task representing the request.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver)
    {
        // No argument guards: the pipeline never passes a null context, and an unregistered resolver
        // fails at DI resolution — guarding here would only defer the throw into the returned Task.
        var tenantId = await tenantResolver.ResolveAsync(context, context.RequestAborted);
        if (tenantId.HasValue)
        {
            TenantContext.SetTenant(tenantId.Value);
        }

        try
        {
            await this.next(context);
        }
        finally
        {
            TenantContext.Clear();
        }
    }
}
