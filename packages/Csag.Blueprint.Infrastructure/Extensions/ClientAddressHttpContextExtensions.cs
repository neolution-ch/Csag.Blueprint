namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Resolves the calling client's address with one rule, shared by everything in the packages that records or
/// keys on it: rate limiting partitions and session tracking.
/// </summary>
public static class ClientAddressHttpContextExtensions
{
    /// <summary>
    /// Resolves the address of the calling client.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item>the value of <paramref name="trustedClientIpHeader"/>, when configured and parseable;</item>
    /// <item><see cref="ConnectionInfo.RemoteIpAddress"/>;</item>
    /// <item>no address at all.</item>
    /// </list>
    /// <para>
    /// A trusted header is required because <see cref="ConnectionInfo.RemoteIpAddress"/> is not the caller's address
    /// behind a load balancer that appends its own <c>X-Forwarded-For</c> entry: the forwarded-headers middleware
    /// reads only the rightmost entry, which is then the balancer or the egress address of an intermediate proxy —
    /// one constant value shared by every caller. The header must be one the edge stamps itself and overwrites on the
    /// way in, so a client cannot forge it.
    /// </para>
    /// </remarks>
    /// <param name="context">The request to identify the client of.</param>
    /// <param name="trustedClientIpHeader">
    /// Name of the edge-stamped header carrying the client address, or <see langword="null"/> to rely on
    /// <see cref="ConnectionInfo.RemoteIpAddress"/> alone.
    /// </param>
    /// <param name="address">The resolved address, or <see langword="null"/> when none could be resolved.</param>
    /// <returns><see langword="true"/> when an address was resolved; otherwise <see langword="false"/>.</returns>
    public static bool TryGetClientAddress(
        this HttpContext context,
        string? trustedClientIpHeader,
        [NotNullWhen(true)] out IPAddress? address)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrWhiteSpace(trustedClientIpHeader)
            && context.Request.Headers.TryGetValue(trustedClientIpHeader, out var headerValues)
            && headerValues.Count > 0)
        {
            // Read the last entry, and the last comma-separated token within it: an edge that appends rather than
            // overwrites leaves its own trustworthy value at the end. The token is sliced off at the final comma
            // rather than split out, because only that one is ever read while the number of entries ahead of it is
            // influenced by the caller — Split would allocate an array proportional to that count on every request.
            // LastIndexOf returns -1 for a comma-free value, which slices the whole string.
            var lastValue = headerValues[^1];
            if (!string.IsNullOrWhiteSpace(lastValue)
                && IPAddress.TryParse(lastValue.AsSpan(lastValue.LastIndexOf(',') + 1).Trim(), out var headerAddress))
            {
                address = headerAddress;
                return true;
            }
        }

        address = context.Connection.RemoteIpAddress;
        return address is not null;
    }
}
