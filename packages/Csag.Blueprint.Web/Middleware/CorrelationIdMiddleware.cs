namespace Csag.Blueprint.Web.Middleware;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Middleware that handles correlation ID for request tracing.
/// Reads X-Correlation-ID or X-Request-ID header from incoming requests (or generates a new one),
/// stores it separately from ASP.NET's TraceIdentifier, enriches logs via NLog scope context properties,
/// and adds X-Correlation-ID header to responses.
/// TraceIdentifier remains unique per request, while CorrelationId can be reused across related requests.
/// </summary>
public class CorrelationIdMiddleware
{
    public static readonly string CorrelationIdKey = "CorrelationId";
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string RequestIdHeader = "X-Request-ID";
    private const int MaxCorrelationIdLength = 100;

    private readonly RequestDelegate next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from headers (prefer X-Correlation-ID over X-Request-ID)
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? context.Request.Headers[RequestIdHeader].FirstOrDefault();

        // If no correlation ID provided, generate a new one
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        correlationId = correlationId.Length > MaxCorrelationIdLength
            ? correlationId[..MaxCorrelationIdLength]
            : correlationId;

        // Store correlation ID in HttpContext.Items for access in application code
        context.Items[CorrelationIdKey] = correlationId;

        // Enrich NLog logs with correlation ID (automatically added to all log entries)
        using (NLog.ScopeContext.PushProperty(CorrelationIdKey, correlationId))
        {
            // Add correlation ID to response headers
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                {
                    context.Response.Headers.Append(CorrelationIdHeader, correlationId);
                }

                return Task.CompletedTask;
            });

            // Continue processing
            await this.next(context);
        }
    }
}
