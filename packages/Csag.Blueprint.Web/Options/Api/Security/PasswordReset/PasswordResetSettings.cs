namespace Csag.Blueprint.Web.Options.Api.Security.PasswordReset
{
    /// <summary>
    /// Configuration settings for password reset functionality.
    /// Controls the token lifetime for password reset links.
    /// </summary>
    public sealed class PasswordResetSettings
    {
        /// <summary>
        /// Gets or sets the lifetime of password reset tokens in minutes.
        /// After this period, the token will be considered expired and the user must request a new one.
        /// </summary>
        public int TokenLifetimeMinutes { get; set; }
    }
}
