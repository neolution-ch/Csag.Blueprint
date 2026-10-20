namespace Csag.Blueprint.Infrastructure.Authentication;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

/// <summary>
/// The one rule for whether a request carries a bearer token, shared by everything in the packages that decides on
/// it: the scheme selectors and the refusal of requests that carry both a session cookie and a bearer token.
/// </summary>
public static class BearerTokenRequestExtensions
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Reads the bearer token from the request's <c>Authorization</c> header.
    /// </summary>
    /// <remarks>
    /// Mirrors how the JWT bearer handler reads it — the whole header value, the <c>Bearer</c> scheme compared without
    /// regard to case, the token trimmed — so a request the selectors forward to a bearer handler is exactly a request
    /// that handler finds a token in.
    /// </remarks>
    /// <param name="request">The request.</param>
    /// <param name="token">The token, or <see langword="null"/> when the request carries none.</param>
    /// <returns><see langword="true"/> when the request carries a non-empty bearer token.</returns>
    public static bool TryGetBearerToken(this HttpRequest request, [NotNullWhen(true)] out string? token)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = authorization[BearerPrefix.Length..].Trim();
            if (candidate.Length > 0)
            {
                token = candidate;
                return true;
            }
        }

        token = null;
        return false;
    }
}
