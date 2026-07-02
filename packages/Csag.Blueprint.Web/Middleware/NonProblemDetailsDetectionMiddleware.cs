namespace Csag.Blueprint.Web.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Development-only middleware that logs a warning when an error response (4xx/5xx)
/// is returned without the application/problem+json content type.
/// This helps catch endpoints that bypass ProblemDetails during development.
/// </summary>
public class NonProblemDetailsDetectionMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly RequestDelegate next;
    private readonly ILogger<NonProblemDetailsDetectionMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonProblemDetailsDetectionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public NonProblemDetailsDetectionMiddleware(RequestDelegate next, ILogger<NonProblemDetailsDetectionMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Invokes the middleware, checking response content type after downstream processing.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        await this.next(context);

        var statusCode = context.Response.StatusCode;
        if (statusCode < 400)
        {
            return;
        }

        var contentType = context.Response.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            // No body written — StatusCodePages or ProblemDetails middleware should handle this
            return;
        }

        if (contentType.StartsWith(ProblemJsonContentType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.logger.LogWarning(
            "Non-ProblemDetails error response detected: {Method} {Path} returned {StatusCode} with Content-Type '{ContentType}'. " +
            "All error responses should use ProblemDetails (RFC 9457).",
            context.Request.Method,
            context.Request.Path,
            statusCode,
            contentType);
    }
}
