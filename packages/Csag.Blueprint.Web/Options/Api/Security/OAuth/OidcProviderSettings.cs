namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System.Collections.Generic;

    /// <summary>
    /// Configuration for a single generic OpenID Connect external login provider.
    /// One entry in <see cref="OAuthSettings.Providers"/>, keyed by its authentication scheme name.
    /// Behavior is tuned by <see cref="Profile"/>; every provider is served by the same generic
    /// <c>AddOpenIdConnect</c> handler (no provider-specific NuGet packages).
    /// </summary>
    public sealed class OidcProviderSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether this provider is enabled.
        /// When false, its authentication scheme and challenge route are not registered.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the built-in provider profile that pre-wires safe defaults.
        /// Defaults to <see cref="OidcProviderProfile.Generic"/>.
        /// </summary>
        public OidcProviderProfile Profile { get; set; } = OidcProviderProfile.Generic;

        /// <summary>
        /// Gets or sets the OIDC authority (issuer base URL). The handler appends
        /// <c>/.well-known/openid-configuration</c> to discover endpoints and signing keys.
        /// Optional for <see cref="OidcProviderProfile.Google"/> (defaults to https://accounts.google.com)
        /// and <see cref="OidcProviderProfile.Entra"/> (derived from <see cref="SignInAudience"/> /
        /// <see cref="TenantId"/>); required for <see cref="OidcProviderProfile.Generic"/>.
        /// Example: "https://accounts.google.com" or "https://your-tenant.auth0.com".
        /// </summary>
        public string? Authority { get; set; }

        /// <summary>
        /// Gets or sets an explicit full discovery-document URL, overriding <see cref="Authority"/>.
        /// Use only for non-standard providers whose metadata is not at
        /// <c>{Authority}/.well-known/openid-configuration</c>.
        /// </summary>
        public string? MetadataAddress { get; set; }

        /// <summary>
        /// Gets or sets the OAuth/OIDC client (application) ID registered with the provider.
        /// Supports GCP Secret Manager syntax: {GoogleSecret:SecretName}.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret registered with the provider (confidential client).
        /// Supports GCP Secret Manager syntax: {GoogleSecret:SecretName}.
        /// IMPORTANT: Never commit this value directly. Use secrets management in production.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the OIDC scopes to request as a semicolon-delimited string.
        /// Must include "openid". Default: "openid;profile;email".
        /// </summary>
        public string Scopes { get; set; } = "openid;profile;email";

        /// <summary>
        /// Gets or sets the human-readable provider name persisted as <c>AspNetUserLogins.LoginProvider</c>
        /// and shown on the sign-in button. Keep stable across deployments so existing external logins keep
        /// matching. Defaults to the provider's scheme key when left null.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the path the provider redirects back to after authentication. Must be unique per
        /// provider and registered as an authorized redirect URI with the provider. Defaults to
        /// "/signin-oidc/{scheme}" when left null.
        /// </summary>
        public string? CallbackPath { get; set; }

        /// <summary>
        /// Gets or sets the Entra Directory (tenant) ID. Required when <see cref="Profile"/> is
        /// <see cref="OidcProviderProfile.Entra"/> and <see cref="SignInAudience"/> is
        /// <see cref="MicrosoftEntraSignInAudience.SingleTenant"/>. Ignored by other profiles.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets which Entra accounts may sign in, driving the authority and issuer validation.
        /// Only used when <see cref="Profile"/> is <see cref="OidcProviderProfile.Entra"/>.
        /// </summary>
        public MicrosoftEntraSignInAudience SignInAudience { get; set; } = MicrosoftEntraSignInAudience.MultiTenant;

        /// <summary>
        /// Gets or sets a value indicating whether sign-in requires a verified email
        /// (<c>email_verified == true</c>). Applies to the <see cref="OidcProviderProfile.Generic"/> and
        /// <see cref="OidcProviderProfile.Google"/> profiles. Default: true.
        /// <para>
        /// The <see cref="OidcProviderProfile.Entra"/> profile computes its own trustworthy value and this
        /// flag is <b>ignored</b> for it: the callback always enforces the profile's computed
        /// <c>email_verified</c> (fail closed) so the "nOAuth" mitigation cannot be switched off.
        /// </para>
        /// </summary>
        public bool RequireVerifiedEmail { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the token issuer is validated. Default: true.
        /// Leave true unless a provider legitimately emits a per-request issuer that cannot be enumerated.
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Gets or sets an explicit allow-list of valid issuers. When set, overrides the single
        /// discovery-derived issuer (useful for providers with multiple valid issuer hosts).
        /// </summary>
        public IList<string>? ValidIssuers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether HTTPS metadata is required. Default: true.
        /// Set false only to allow a local http identity provider during development.
        /// </summary>
        public bool RequireHttpsMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to fetch additional claims from the userinfo endpoint.
        /// Default: true. Useful when a provider omits profile/email claims from the id_token.
        /// </summary>
        public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

        /// <summary>
        /// Gets or sets the OIDC <c>prompt</c> parameter sent on every authorization request.
        /// Default: "select_account", which forces the provider to show its account chooser instead of
        /// silently reusing an already-signed-in session. Set to null/empty to let the provider decide,
        /// or use another standard value ("login", "consent", "none"). Applies to all profiles.
        /// </summary>
        public string? Prompt { get; set; } = "select_account";
    }
}
