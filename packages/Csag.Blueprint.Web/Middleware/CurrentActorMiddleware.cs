namespace Csag.Blueprint.Web.Middleware;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Application.Services;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that sets the ambient current-actor context from the authenticated principal.
/// Resolves a readable actor label — the user's email for real accounts, the client id for service
/// accounts — and stores it in <see cref="CurrentActorContext"/> (AsyncLocal) for the
/// duration of the request, so the audit save interceptor can stamp <c>CreatedByActor</c> /
/// <c>UpdatedByActor</c> without holding a scoped dependency.
/// <para>
/// Mirrors <c>TenantMiddleware</c>. When there is no authenticated actor (or no resolvable label)
/// the context stays unset (null) and the audit columns are left null.
/// </para>
/// </summary>
public class CurrentActorMiddleware
{
    private readonly RequestDelegate next;

    public CurrentActorMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var actor = ResolveActor(context.User);

            if (!string.IsNullOrEmpty(actor))
            {
                // Set ambient current-actor context for this request.
                CurrentActorContext.SetActor(actor);
            }
        }

        try
        {
            await this.next(context);
        }
        finally
        {
            // Clear current-actor context after the request completes.
            CurrentActorContext.Clear();
        }
    }

    /// <summary>
    /// Resolves the actor label to stamp for the given principal: the user's email (a readable
    /// point-in-time snapshot) for real accounts, or the client id for service accounts.
    /// Returns null when neither is available.
    /// </summary>
    private static string? ResolveActor(ClaimsPrincipal user)
    {
        // Real accounts carry their email as a claim — the readable snapshot we stamp on the row.
        var email = user.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            return email;
        }

        // Service accounts have no email; stamp them with their client id. The package's bearer scheme builds the
        // principal with the raw claim names, so the client id is always under ServiceAccountClaimTypes.Subject.
        if (string.Equals(user.FindFirstValue(ServiceAccountClaimTypes.Type), ServiceAccountClaimTypes.ServiceAccountType, StringComparison.Ordinal))
        {
            return user.FindFirstValue(ServiceAccountClaimTypes.Subject);
        }

        return null;
    }
}
