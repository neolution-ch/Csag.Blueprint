namespace Csag.Blueprint.Web.Middleware;

using Audit.Core;
using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Http;

/// <summary>
/// This middleware records an audit event for a denied HTTP request.
/// A successful request produces no event. The EF Core interceptor already audits the writes.
/// GCP and Application Insights already record general request data, for example the method, the
/// URL, the status code, and the duration. This audit log does not need that data.
/// </summary>
/// <remarks>
/// Register this middleware before <c>app.UseBlueprintMiddleware()</c>: its <c>try</c>/<c>finally</c>
/// block must wrap authentication and authorization, because the ASP.NET Core authorization
/// middleware does not call the next middleware for a denied request.
/// </remarks>
public class AuthorizationAuditMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationAuditMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public AuthorizationAuditMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// This method processes an HTTP request. It creates an audit scope for the request. The scope
    /// survives only when the final response status is 401 or 403.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="tenantResolver">
    /// This middleware runs before <see cref="TenantMiddleware"/> (see the class remarks), so the
    /// ambient <see cref="Csag.Blueprint.Application.Services.TenantContext"/> is not set yet. This
    /// parameter resolves the tenant directly instead.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver)
    {
        var path = context.Request.Path.Value;
        if (path != null && (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)))
        {
            await this.next(context);
            return;
        }

        var eventType = $"HTTP:{context.Request.Method}:{context.Request.Path}";
        if (eventType.Length > 100)
        {
            eventType = eventType[..100];
        }

        // Create the scope before authentication and authorization run.
        // This try/finally block still executes when authorization denies the request later in the pipeline.
        await using var scope = await AuditScope.CreateAsync(new AuditScopeOptions
        {
            EventType = eventType,
        });

        try
        {
            await this.next(context);
        }
        finally
        {
            if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            {
                var actor = AuditUserIdentity.FromPrincipal(context.User);
                var tenantId = await tenantResolver.ResolveAsync(context, context.RequestAborted);
                var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var cid)
                    ? cid?.ToString() : null;

                scope.SetCustomField("StatusCode", context.Response.StatusCode);
                scope.SetCustomField("UserId", actor.UserId);
                scope.SetCustomField("TenantId", tenantId);
                scope.SetCustomField("UserEmail", actor.Email);
                scope.SetCustomField("UserDisplayName", actor.DisplayName);
                scope.SetCustomField(CorrelationIdMiddleware.CorrelationIdKey, correlationId);
                scope.SetCustomField("UserAgent", context.Request.Headers.UserAgent.ToString());
            }
            else
            {
                // Not a denied request: discard the scope so it never reaches the audit log.
                scope.Discard();
            }
        }

        // A kept scope is saved on DisposeAsync (await using).
    }
}
