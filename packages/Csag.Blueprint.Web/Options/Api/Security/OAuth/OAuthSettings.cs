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
        /// Gets or sets the absolute base URL of the frontend application (scheme + host, e.g. "https://localhost:20023").
        /// External-auth flows always complete on the API origin, because the OAuth provider's redirect URI points at the
        /// API rather than the frontend. The post-login redirect must therefore be sent back to the frontend origin,
        /// otherwise the user lands on the API host (where the SPA routes do not exist → 404).
        /// When null, redirects use a relative path, which is only correct when the frontend and API share an origin.
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
