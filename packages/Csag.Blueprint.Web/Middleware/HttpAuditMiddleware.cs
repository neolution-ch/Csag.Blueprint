namespace Csag.Blueprint.Web.Middleware;

using System.Diagnostics;
using Audit.Core;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Web.Helpers;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that creates an audit event for each HTTP request.
/// Captures method, URL, status code, user identity, correlation ID, and request duration.
/// Skips health check and swagger endpoints to reduce noise.
/// </summary>
public class HttpAuditMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpAuditMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public HttpAuditMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// Processes an HTTP request by wrapping it in an audit scope.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="tenantService">Service used to retrieve the current tenant identifier.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
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

        // Create the audit scope before the request so the scope is open during processing
        await using var scope = await AuditScope.CreateAsync(new AuditScopeOptions
        {
            EventType = eventType,
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await this.next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Enrich the scope with response data after the request completes
            var actor = AuditUserIdentity.FromPrincipal(context.User);
            var userType = context.User?.FindFirst("type")?.Value ?? "Unknown";
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var cid)
                ? cid?.ToString() : null;

            scope.SetCustomField("HttpMethod", context.Request.Method);

            // Query string is intentionally excluded to prevent logging tokens or PII passed via URL.
            scope.SetCustomField("Url", $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}");
            scope.SetCustomField("StatusCode", context.Response.StatusCode);
            scope.SetCustomField("DurationMs", stopwatch.ElapsedMilliseconds);
            scope.SetCustomField("UserId", actor.UserId);
            scope.SetCustomField("TenantId", tenantService.CurrentTenantId);

            // Set these two fields here also, not only in the global OnScopeCreated enrichment. This
            // scope is saved after the request ends. Without these lines, an HTTP entry and an Entity
            // Framework entry for the same request can show a different user.
            scope.SetCustomField("UserEmail", actor.Email);
            scope.SetCustomField("UserDisplayName", actor.DisplayName);
            scope.SetCustomField("UserType", userType);
            scope.SetCustomField(CorrelationIdMiddleware.CorrelationIdKey, correlationId);
            scope.SetCustomField("UserAgent", context.Request.Headers.UserAgent.ToString());
        }

        // Scope is saved on DisposeAsync (await using)
    }
}
