namespace Csag.Blueprint.Web.Helpers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Helper methods for OAuth authentication flows.
/// </summary>
public static class OAuthHelpers
{
    /// <summary>
    /// The query-string key used to surface an external-auth error code back to the frontend
    /// (e.g. <c>?error=email_not_verified</c>). Shared by the challenge and callback endpoints so the
    /// error contract cannot drift between them. Exposed as a static field rather than a public const so a
    /// future change is not baked into consuming assemblies at compile time.
    /// </summary>
    public static readonly string ErrorQueryParameter = "error";

    /// <summary>
    /// The query-string key carrying the email address an external sign-in actually returned when it
    /// failed the flow's expected-email pin (<c>error=email_mismatch</c>), so the frontend can name the
    /// address that was used. Exposed as a static field for the same reason as
    /// <see cref="ErrorQueryParameter"/>.
    /// </summary>
    public static readonly string ErrorEmailQueryParameter = "errorEmail";

    /// <summary>
    /// The <c>AuthenticationProperties.Items</c> key carrying the address an external-auth flow is
    /// pinned to (e.g. the invited address during an invitation/onboarding flow). Stamped by the
    /// challenge endpoint and read back by the callback; shared so the two sides of the round trip
    /// cannot drift apart — a key typo would silently disable the pin.
    /// </summary>
    public static readonly string ExpectedEmailPropertyKey = "expectedEmail";

    /// <summary>
    /// Validates that a return URL is local and safe to redirect to.
    /// Returns "/" if the URL is null, empty, or not a local path.
    /// </summary>
    /// <param name="returnUrl">The return URL to validate.</param>
    /// <param name="logger">Logger instance for warning messages.</param>
    /// <returns>A safe local path.</returns>
    public static string ValidateLocalPath(string? returnUrl, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        // Try to parse as a relative URI to ensure it's well-formed
        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            logger.LogWarning("Invalid URL format rejected: {ReturnUrl}", returnUrl);
            return "/";
        }

        // Ensure the URL is local (a single leading '/') and doesn't smuggle in an authority or scheme.
        // Mirrors ASP.NET Core's IsLocalUrl, which rejects both "//host" and the backslash form "/\host"
        // (browsers normalise "\" to "/", so "/\evil.com" would resolve to the protocol-relative
        // "//evil.com"). Rejecting it here keeps the guard's output consistent with the downstream
        // LocalRedirect, so a crafted returnUrl falls back to "/" instead of surfacing as a 500.
        if (returnUrl.StartsWith('/') &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("/\\", StringComparison.Ordinal) &&
            !returnUrl.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        logger.LogWarning("Invalid or non-local return URL rejected: {ReturnUrl}", returnUrl);
        return "/";
    }

    /// <summary>
    /// Builds the post-authentication redirect target. External-auth flows complete on the API origin
    /// (the OAuth provider's redirect URI points at the API), so when a trusted frontend base URL is
    /// configured the validated local path is appended to it, returning the browser to the frontend origin.
    /// Falls back to the local path unchanged when no base URL is set (same-origin deployments).
    /// </summary>
    /// <param name="frontendBaseUrl">The trusted, server-configured frontend base URL (scheme + host), or null.</param>
    /// <param name="localPath">A safe local path produced by <see cref="ValidateLocalPath"/> (always starts with "/").</param>
    /// <returns>An absolute frontend URL when a base URL is configured; otherwise the local path.</returns>
    public static string BuildPostAuthRedirect(string? frontendBaseUrl, string localPath)
    {
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            return localPath;
        }

        return $"{frontendBaseUrl.TrimEnd('/')}{localPath}";
    }
}
