namespace Csag.Blueprint.Web.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware that gracefully handles <see cref="OperationCanceledException"/> thrown when
/// an HTTP request is aborted by the client (e.g., browser navigates away, connection drops).
/// Without this middleware, the cancelled EF Core / async operations bubble up as unhandled
/// exceptions and get logged at Error level with a 500 status code.
/// This middleware catches them early, logs at Information level, and returns HTTP 499 (Client Closed Request).
/// </summary>
public sealed class OperationCancelledMiddleware
{
    /// <summary>
    /// HTTP 499 Client Closed Request (nginx convention, widely adopted).
    /// </summary>
    private const int StatusCodeClientClosedRequest = 499;

    private readonly RequestDelegate next;
    private readonly ILogger<OperationCancelledMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationCancelledMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public OperationCancelledMiddleware(RequestDelegate next, ILogger<OperationCancelledMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// Invokes the middleware. Catches <see cref="OperationCanceledException"/> when
    /// the request is cancelled and returns an appropriate response.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await this.next(context);
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            this.logger.LogInformation(
                ex,
                "Request was cancelled by the client: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            // Set status code only if response has not already started
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodeClientClosedRequest;
            }
        }
    }
}
