namespace Csag.Blueprint.Web.Middleware;

using System.Diagnostics;
using System.Security.Claims;
using Audit.Core;
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
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
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userType = context.User?.FindFirst("type")?.Value ?? "Unknown";
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var cid)
                ? cid?.ToString() : null;

            scope.SetCustomField("HttpMethod", context.Request.Method);

            // Query string is intentionally excluded to prevent logging tokens or PII passed via URL.
            scope.SetCustomField("Url", $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}");
            scope.SetCustomField("StatusCode", context.Response.StatusCode);
            scope.SetCustomField("DurationMs", stopwatch.ElapsedMilliseconds);
            scope.SetCustomField("UserId", userId);
            scope.SetCustomField("UserType", userType);
            scope.SetCustomField(CorrelationIdMiddleware.CorrelationIdKey, correlationId);
            scope.SetCustomField("UserAgent", context.Request.Headers.UserAgent.ToString());
        }

        // Scope is saved on DisposeAsync (await using)
    }
}
