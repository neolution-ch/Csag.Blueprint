namespace Csag.Blueprint.Web.Options.Api.Security.Jwt
{
    /// <summary>
    /// Configuration settings for JWT authentication used by service accounts.
    /// Nested under ApiSettings:Security:Jwt in appsettings.
    /// </summary>
    public sealed class JwtSettings
    {
        /// <summary>
        /// Gets or sets the signing key used to sign and validate JWT tokens.
        /// Must be at least 32 characters long for HS256. Store securely via user secrets or environment variables.
        /// </summary>
        public string SigningKey { get; set; } = null!;

        /// <summary>
        /// Gets or sets the issuer of the JWT token.
        /// </summary>
        public string Issuer { get; set; } = null!;

        /// <summary>
        /// Gets or sets the audience of the JWT token.
        /// </summary>
        public string Audience { get; set; } = null!;

        /// <summary>
        /// Gets or sets the token expiration time in hours.
        /// </summary>
        public int ExpirationHours { get; set; }
    }
}
