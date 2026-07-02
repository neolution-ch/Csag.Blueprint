namespace Csag.Blueprint.Web.Middleware;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Application.Services;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that sets the ambient tenant context from authenticated user claims.
/// Reads the TenantId claim and sets it in TenantContext (AsyncLocal) for the duration of the request.
/// This ensures query filters and save interceptors have access to the current tenant.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate next;

    public TenantMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Read tenant ID from authenticated user's claims
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirstValue(IdentityClaimTypes.TenantId);

            if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, CultureInfo.InvariantCulture, out var tenantId))
            {
                // Set ambient tenant context for this request
                TenantContext.SetTenant(tenantId);
            }
        }

        try
        {
            await this.next(context);
        }
        finally
        {
            // Clear tenant context after request completes
            TenantContext.Clear();
        }
    }
}
