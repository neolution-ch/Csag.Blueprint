namespace Csag.Blueprint.Web.Options.Api.Security.PasswordReset
{
    /// <summary>
    /// Configuration settings for password reset functionality.
    /// Controls the frontend URL for reset links and token lifetime.
    /// </summary>
    public sealed class PasswordResetSettings
    {
        /// <summary>
        /// Gets or sets the base URL of the frontend page that handles password resets.
        /// The reset token and email will be appended as query parameters.
        /// Example: "https://app.example.com/reset-password".
        /// </summary>
        public string? FrontendBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the lifetime of password reset tokens in minutes.
        /// After this period, the token will be considered expired and the user must request a new one.
        /// </summary>
        public int TokenLifetimeMinutes { get; set; }
    }
}
