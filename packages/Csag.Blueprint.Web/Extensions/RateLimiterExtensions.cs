namespace Csag.Blueprint.Web.Extensions;

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Extension methods for wiring ASP.NET Core rate limiting into a Blueprint application:
/// resolving the client partition key and shaping the rejection response.
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Resolves the key identifying the calling client, used to partition a rate limiter.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item>the value of <paramref name="trustedClientIpHeader"/>, when configured and parseable;</item>
    /// <item><see cref="ConnectionInfo.RemoteIpAddress"/>;</item>
    /// <item>no key at all.</item>
    /// </list>
    /// <para>
    /// A trusted header is required because <see cref="ConnectionInfo.RemoteIpAddress"/> is not the
    /// caller's address behind a load balancer that appends its own <c>X-Forwarded-For</c> entry: the
    /// forwarded-headers middleware reads only the rightmost entry, which is then the balancer or the
    /// egress address of an intermediate proxy — one constant value shared by every caller. The header
    /// must be one the edge stamps itself and overwrites on the way in, so a client cannot forge it.
    /// </para>
    /// <para>
    /// Callers MUST map a <see langword="false"/> result to
    /// <see cref="RateLimitPartition.GetNoLimiter{TKey}(TKey)"/> and MUST NOT substitute a placeholder
    /// key: a shared placeholder bucket turns a failure to identify the caller into a rate-limit
    /// rejection for every caller at once.
    /// </para>
    /// </remarks>
    /// <param name="context">The request to identify the client of.</param>
    /// <param name="trustedClientIpHeader">
    /// Name of the edge-stamped header carrying the client address, or <see langword="null"/> to rely on
    /// <see cref="ConnectionInfo.RemoteIpAddress"/> alone.
    /// </param>
    /// <param name="partitionKey">The resolved key, or an empty string when none could be resolved.</param>
    /// <returns><see langword="true"/> when a key was resolved; otherwise <see langword="false"/>.</returns>
    public static bool TryGetClientPartitionKey(this HttpContext context, string? trustedClientIpHeader, out string partitionKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrWhiteSpace(trustedClientIpHeader)
            && context.Request.Headers.TryGetValue(trustedClientIpHeader, out var headerValues)
            && headerValues.Count > 0)
        {
            // Read the last entry, and the last comma-separated token within it: an edge that appends
            // rather than overwrites leaves its own trustworthy value at the end. The token is sliced off
            // at the final comma rather than split out, because only that one is ever read while the number
            // of entries ahead of it is influenced by the caller — Split would allocate an array
            // proportional to that count on every request. LastIndexOf returns -1 for a comma-free value,
            // which slices the whole string.
            var lastValue = headerValues[^1];
            if (!string.IsNullOrWhiteSpace(lastValue)
                && IPAddress.TryParse(lastValue.AsSpan(lastValue.LastIndexOf(',') + 1).Trim(), out var headerAddress))
            {
                partitionKey = NormalizeAddress(headerAddress);
                return true;
            }
        }

        var remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is not null)
        {
            partitionKey = NormalizeAddress(remoteAddress);
            return true;
        }

        partitionKey = string.Empty;
        return false;
    }

    /// <summary>
    /// Configures the rate limiter to reject with <c>429 Too Many Requests</c> and an RFC 9457
    /// ProblemDetails body carrying the correlation ID, plus a <c>Retry-After</c> header when the
    /// limiter reports one.
    /// </summary>
    /// <remarks>
    /// The framework default for <see cref="RateLimiterOptions.RejectionStatusCode"/> is
    /// <c>503 Service Unavailable</c>, which reads as a backend fault to an upstream load balancer.
    /// </remarks>
    /// <param name="options">The rate limiter options to configure.</param>
    /// <returns>The same options instance, for chaining.</returns>
    public static RateLimiterOptions UseBlueprintRejectionResponse(this RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (rejected, cancellationToken) =>
        {
            var context = rejected.HttpContext;

            if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
            }

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;

            var problemDetails = new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too many requests",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Too many requests have been sent from this client. Retry later.",
                correlationId,
            };

            await context.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken);
        };

        return options;
    }

    /// <summary>
    /// Collapses an IPv4-mapped IPv6 address to its IPv4 form so the same caller always lands in one
    /// partition regardless of the form the address arrived in.
    /// </summary>
    /// <param name="address">The address to normalize.</param>
    /// <returns>The normalized address in string form.</returns>
    private static string NormalizeAddress(IPAddress address) =>
        (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}
