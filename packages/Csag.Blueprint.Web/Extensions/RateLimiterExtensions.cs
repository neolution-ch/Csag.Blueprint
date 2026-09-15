namespace Csag.Blueprint.Web.Extensions;

using System.Globalization;
using System.Net;
using System.Net.Sockets;
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
    // Appended to a truncated IPv6 key so the value reads as the prefix it is rather than as a host
    // address, in a log line or a rejection diagnostic.
    private const string IPv6PrefixSuffix = "/64";

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
    /// <para>
    /// The key is opaque, not an address: an IPv4 caller is keyed by their address, an IPv6 caller by
    /// their <c>/64</c> prefix rendered as <c>2001:db8:85a3:8d3::/64</c>. See
    /// <see cref="NormalizeAddress"/>.
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

            // RFC 9110 delay-seconds is an unsigned integer, so a negative delay has no encoding at all.
            // Omit the header rather than clamp it to zero: a lease reporting a negative delay is already
            // out of contract, and "0" tells a well-behaved client to retry immediately against the very
            // limiter that just rejected it, discarding the backoff a bare 429 would have earned. The zero
            // the window limiters genuinely emit still serializes as "0".
            //
            // TokenBucketRateLimiter reaches both edges with stock options: it derives this metadata from an
            // unchecked ReplenishmentPeriod.Ticks multiply, which goes negative once the periods needed
            // overflow Int64 (TokenLimit 1e9, TokensPerPeriod 1, period 1h), and stays positive but far past
            // Int32 seconds below that. Seconds are therefore widened to long, where an out-of-range
            // double-to-int conversion has no defined result.
            if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) && retryAfter >= TimeSpan.Zero)
            {
                context.Response.Headers.RetryAfter = ((long)Math.Ceiling(retryAfter.TotalSeconds))
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
    /// Reduces an address to the key one caller is partitioned by: an IPv4-mapped IPv6 address
    /// collapses to its IPv4 form, a native IPv6 address to its <c>/64</c> prefix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A mapped address collapses to IPv4 so the same caller always lands in one partition regardless
    /// of the form the address arrived in.
    /// </para>
    /// <para>
    /// A native IPv6 caller is allocated a prefix, not an address: the smallest block normally routed
    /// to a customer is a /64, and all 2^64 addresses in it are theirs to send from at no cost. Keyed
    /// on the full /128 a limiter therefore enforces nothing against a caller who varies the source
    /// address within their own prefix, while an IPv4 caller behind NAT shares one bucket with their
    /// whole office. Truncating to the /64 restores that symmetry. It does not make the evasion
    /// impossible — a caller holding several prefixes still gets one bucket each — but it prices it at
    /// one routed prefix per bucket instead of at nothing.
    /// </para>
    /// <para>
    /// The result is an opaque partition key, not an address: the <c>/64</c> suffix keeps a truncated
    /// prefix from reading as a host address in a log line, and truncation drops the scope id, so two
    /// link-local callers reached over different interfaces share a key.
    /// </para>
    /// </remarks>
    /// <param name="address">The address to normalize.</param>
    /// <returns>The partition key for the address.</returns>
    private static string NormalizeAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        // Only an IPv6 address reaches here, and TryWriteBytes fails only on a destination shorter than
        // the address, so a 16-byte destination makes the write unconditional. Clearing the trailing
        // eight bytes leaves the leading 64 bits, which IPAddress.ToString renders in canonical
        // compressed form.
        Span<byte> bytes = stackalloc byte[16];
        _ = address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();

        return string.Concat(new IPAddress(bytes).ToString(), IPv6PrefixSuffix);
    }
}
