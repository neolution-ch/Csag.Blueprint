namespace Csag.Blueprint.Web.Middleware;

using System.Security.Claims;
using Csag.Blueprint.Application.Services;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that sets the ambient current-actor context from the authenticated principal.
/// Resolves a readable actor label — the user's email for real accounts, the <c>sa-{clientId}</c>
/// for service accounts — and stores it in <see cref="CurrentActorContext"/> (AsyncLocal) for the
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
    /// point-in-time snapshot) for real accounts, or the <c>sa-{clientId}</c> for service accounts.
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

        // Service accounts have no email; stamp them with their client id (already "sa-"-prefixed).
        // The client id is the token's "sub"; fall back to the mapped NameIdentifier claim in case
        // JWT inbound claim mapping renamed "sub".
        if (string.Equals(user.FindFirstValue("type"), "ServiceAccount", StringComparison.Ordinal))
        {
            return user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        return null;
    }
}
