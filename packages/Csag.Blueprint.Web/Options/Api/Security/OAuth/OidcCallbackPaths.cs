namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System;

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
    }
}
