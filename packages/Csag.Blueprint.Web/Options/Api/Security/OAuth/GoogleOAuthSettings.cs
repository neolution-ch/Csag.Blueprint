namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    /// <summary>
    /// Configuration settings for Google OAuth authentication.
    /// See https://developers.google.com/identity/protocols/oauth2 for setup instructions.
    /// </summary>
    public sealed class GoogleOAuthSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether Google OAuth authentication is enabled.
        /// When false, Google OAuth endpoints will not be available.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the Google OAuth Client ID.
        /// Obtain from Google Cloud Console: https://console.cloud.google.com/apis/credentials.
        /// Supports GCP Secret Manager syntax: {GoogleSecret:GoogleClientSecret}.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the Google OAuth Client Secret.
        /// Obtain from Google Cloud Console: https://console.cloud.google.com/apis/credentials.
        /// Supports GCP Secret Manager syntax: {GoogleSecret:GoogleClientSecret}.
        /// IMPORTANT: Never commit this value directly. Use secrets management in production.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the OAuth scopes to request from Google as a semicolon-delimited string.
        /// Default scopes: "openid;profile;email".
        /// Additional scopes for API access (e.g., "https://www.googleapis.com/auth/calendar.readonly").
        /// See https://developers.google.com/identity/protocols/oauth2/scopes for available scopes.
        /// </summary>
        public string Scopes { get; set; } = null!;
    }
}
