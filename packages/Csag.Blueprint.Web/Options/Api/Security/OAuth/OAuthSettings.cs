namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    /// <summary>
    /// OAuth external authentication provider settings.
    /// Configures integration with third-party identity providers (Google, Microsoft, GitHub, etc.).
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
        /// Gets or sets the default role to assign to automatically created OAuth users.
        /// This role is only applied when AutoCreateUsers is true and a new user account is created.
        /// Must be a valid role name from Application.Authorization.Roles (e.g., "Admin", "Manager", "Employee", "Maintenance").
        /// Required when AutoCreateUsers is true.
        /// Example: "Employee" - gives basic access to new OAuth users.
        /// </summary>
        public string? DefaultRole { get; set; }

        /// <summary>
        /// Gets or sets the Google OAuth authentication settings.
        /// Configure this to enable "Sign in with Google" functionality.
        /// </summary>
        public GoogleOAuthSettings Google { get; set; } = new();
    }
}
