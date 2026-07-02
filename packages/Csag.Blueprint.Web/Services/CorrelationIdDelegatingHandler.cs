namespace Csag.Blueprint.Web.Services;

using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;

/// <summary>
/// HTTP message handler that propagates correlation IDs to outbound HTTP requests.
/// Reads the correlation ID from HttpContext.Items and adds it as X-Correlation-ID header.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly IHttpContextAccessor httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get correlation ID from current HTTP context (if available)
        var correlationId = this.httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;

        // Add correlation ID header to outbound request if we have one
        // This enables end-to-end tracing across service boundaries
        if (!string.IsNullOrWhiteSpace(correlationId) && !request.Headers.Contains(CorrelationIdHeader))
        {
            request.Headers.Add(CorrelationIdHeader, correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
