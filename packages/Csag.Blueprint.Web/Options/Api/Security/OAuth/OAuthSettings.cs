namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// OAuth external authentication settings.
    /// Configures generic OpenID Connect integration with one or more third-party identity providers
    /// (Google, Microsoft Entra ID, and any compliant OIDC provider) via their well-known discovery URLs.
    /// </summary>
    public sealed class OAuthSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to automatically create user accounts when a user signs in via OAuth
        /// and no matching account exists in the database.
        /// When true: New users are automatically created with information from the OAuth provider.
        /// When false: Only existing users (matched by email) can sign in via OAuth. New users must register first.
        /// Recommended: false for enterprise scenarios where users are pre-provisioned.
        /// </summary>
        public bool AutoCreateUsers { get; set; }

        /// <summary>
        /// Gets or sets the absolute base URL of the frontend application (scheme + host, e.g. "https://localhost:20023"),
        /// whose origin forwards <see cref="OidcCallbackPaths.ProxiedPrefix"/> to the API. When set, the post-login
        /// redirect targets it, and every provider returns the user to it: the provider's redirect URI is this origin plus
        /// the scheme's callback path, which must therefore sit under that prefix. Set it wherever a proxy between browser
        /// and API rewrites the Host header, because the redirect URI the handler would build on its own names the proxy's
        /// upstream host. When null, redirects stay relative and the redirect URI uses the request's host, which is only
        /// correct when the API itself receives the browser's origin.
        /// </summary>
        public string? FrontendBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the configured OpenID Connect providers, keyed by authentication scheme name
        /// (e.g. "google", "microsoft"). Each enabled entry is registered as its own OIDC scheme and
        /// exposed at <c>/auth/external/{scheme}</c>. Configure entries to enable "Sign in with ..." options.
        /// </summary>
        public IDictionary<string, OidcProviderSettings> Providers { get; set; }
            = new Dictionary<string, OidcProviderSettings>(StringComparer.OrdinalIgnoreCase);
    }
}
