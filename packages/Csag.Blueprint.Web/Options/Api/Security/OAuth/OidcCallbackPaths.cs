namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System;
    using System.Linq;

    /// <summary>
    /// Resolves the path each OpenID Connect provider returns to after authentication. Registration and
    /// validation both read it from here, so the path a scheme listens on is the path that was validated.
    /// </summary>
    public static class OidcCallbackPaths
    {
        /// <summary>
        /// Gets the path prefix the frontend origin forwards to the API. When <see cref="OAuthSettings.FrontendBaseUrl"/>
        /// is set, providers return the user to that origin, so their callback paths must sit under this prefix to
        /// reach the API at all.
        /// </summary>
        public static string ProxiedPrefix { get; } = "/api/";

        /// <summary>
        /// Returns the provider's configured callback path, or <c>/api/auth/signin-oidc/{scheme}</c> when none is set.
        /// </summary>
        /// <param name="scheme">The authentication scheme name (the provider's configuration key).</param>
        /// <param name="provider">The provider settings.</param>
        /// <returns>The effective callback path.</returns>
        public static string Resolve(string scheme, OidcProviderSettings provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            return string.IsNullOrWhiteSpace(provider.CallbackPath)
                ? $"/api/auth/signin-oidc/{scheme}"
                : provider.CallbackPath;
        }

        /// <summary>
        /// Returns whether a callback path sits under <see cref="ProxiedPrefix"/> as written. A proxy decodes and
        /// normalizes a path before routing it, so a path with empty, <c>.</c> or <c>..</c> segments, percent-encoding
        /// or a backslash could be routed somewhere its prefix does not suggest, and does not count. Neither does one
        /// carrying a query or fragment: the handler matches its callback path against the request path alone.
        /// </summary>
        /// <param name="path">The callback path.</param>
        /// <returns>True when the path is plain, normalized and under the proxied prefix.</returns>
        public static bool IsUnderProxiedPrefix(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            if (!path.StartsWith(ProxiedPrefix, StringComparison.Ordinal) || path.IndexOfAny(['\\', '%', '?', '#']) >= 0)
            {
                return false;
            }

            return path.Split('/').Skip(1).All(segment => segment.Length > 0 && segment is not "." and not "..");
        }
    }
}
