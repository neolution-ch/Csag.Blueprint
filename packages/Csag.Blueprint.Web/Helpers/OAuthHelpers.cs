namespace Csag.Blueprint.Web.Helpers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Helper methods for OAuth authentication flows.
/// </summary>
public static class OAuthHelpers
{
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

        // Ensure the URL is local (starts with /) and doesn't contain schemes or authority
        // Prevents open redirects to external sites
        if (returnUrl.StartsWith('/') &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        logger.LogWarning("Invalid or non-local return URL rejected: {ReturnUrl}", returnUrl);
        return "/";
    }
}
