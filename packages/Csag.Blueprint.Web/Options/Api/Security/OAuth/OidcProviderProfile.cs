namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    /// <summary>
    /// Selects the built-in configuration profile applied to a generic OpenID Connect provider.
    /// A profile pre-wires safe, provider-appropriate defaults (authority, claim handling, issuer
    /// validation, email-trust policy) on top of the generic <c>AddOpenIdConnect</c> handler, so the
    /// same code path serves multiple identity providers without provider-specific NuGet packages.
    /// </summary>
    public enum OidcProviderProfile
    {
        /// <summary>
        /// A standards-compliant OpenID Connect provider (Auth0, Okta, Keycloak, etc.).
        /// Uses <see cref="OidcProviderSettings.Authority"/> / <see cref="OidcProviderSettings.MetadataAddress"/>
        /// for discovery, the default inbound claim mapping, and trusts <c>email_verified</c> from the token.
        /// </summary>
        Generic,

        /// <summary>
        /// Google. Defaults the authority to <c>https://accounts.google.com</c> and relies on Google's
        /// standard <c>email_verified</c> claim; otherwise identical to <see cref="Generic"/>.
        /// </summary>
        Google,

        /// <summary>
        /// Microsoft Entra ID (Azure AD). Derives the authority from
        /// <see cref="OidcProviderSettings.SignInAudience"/> (+ <see cref="OidcProviderSettings.TenantId"/>),
        /// applies tenant-aware issuer validation, and computes a trustworthy <c>email_verified</c> claim
        /// (single-tenant, or the optional <c>xms_edov</c> claim) to avoid the "nOAuth" account-takeover class.
        /// </summary>
        Entra,
    }
}
