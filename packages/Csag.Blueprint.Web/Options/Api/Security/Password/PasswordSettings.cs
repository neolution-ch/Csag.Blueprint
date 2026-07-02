namespace Csag.Blueprint.Web.Options.Api.Security.Password
{
    /// <summary>
    /// Password policy configuration settings.
    /// Used to configure ASP.NET Core Identity password requirements.
    /// </summary>
    public sealed class PasswordSettings
    {
        /// <summary>
        /// Gets or sets the minimum length required for passwords.
        /// </summary>
        public int RequiredLength { get; set; }

        /// <summary>
        /// Gets or sets the number of unique characters required in passwords.
        /// </summary>
        public int RequiredUniqueChars { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether passwords must contain at least one digit.
        /// </summary>
        public bool RequireDigit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether passwords must contain at least one lowercase letter.
        /// </summary>
        public bool RequireLowercase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether passwords must contain at least one uppercase letter.
        /// </summary>
        public bool RequireUppercase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether passwords must contain at least one non-alphanumeric character.
        /// </summary>
        public bool RequireNonAlphanumeric { get; set; }
    }
}
