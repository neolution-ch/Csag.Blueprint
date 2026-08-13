namespace Csag.Blueprint.Web.Options.Api.Security
{
    /// <summary>
    /// Multi-factor authentication (MFA) configuration settings.
    /// Controls whether time-based one-time password (TOTP) MFA is supported, whether it is mandatory
    /// for all users, and which external identity providers are trusted enough to bypass the MFA challenge.
    /// </summary>
    public sealed class MfaSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether MFA is supported at all.
        /// When <c>false</c>, users cannot register or enable MFA and no one is ever challenged for a
        /// second factor — the login flow behaves exactly as it did before MFA existed.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether MFA is required for every user.
        /// Only valid when <see cref="Enabled"/> is <c>true</c>. When <c>true</c>, users without a
        /// configured authenticator are forced through MFA setup before they can use the application.
        /// When <c>false</c>, MFA is optional and users may opt in.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the list of external login provider names (for example "Google" or
        /// "Microsoft") whose users bypass the MFA challenge. Such providers are already considered a
        /// strong second factor / trusted identity provider, so no additional TOTP challenge is issued.
        /// Matching is case-insensitive.
        /// </summary>
        // Kept as an empty-list default (not null!) because "bypass no providers" is the safe default and an
        // empty JSON array in appsettings binds to nothing — it cannot override a null default — so a null!
        // default would fail startup even when the key is present. Empty is the intended, secure default.
        public IList<string> BypassExternalProviders { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the issuer label embedded in the <c>otpauth://</c> URI and shown by authenticator
        /// apps (for example "CSAG Blueprint"). The default lives in appsettings.json, not here, so a missing
        /// configuration value fails validation at startup rather than silently substituting a default.
        /// </summary>
        public string Issuer { get; set; } = null!;

        /// <summary>
        /// Gets or sets the number of single-use recovery codes generated when a user enables MFA or
        /// regenerates their codes. Each code can be used once to satisfy the second-factor challenge when
        /// the authenticator device is unavailable. Only relevant when <see cref="Enabled"/> is <c>true</c>.
        /// </summary>
        public int RecoveryCodeCount { get; set; }
    }
}
