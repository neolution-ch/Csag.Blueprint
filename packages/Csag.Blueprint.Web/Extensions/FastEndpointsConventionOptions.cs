namespace Csag.Blueprint.Web.Extensions
{
    /// <summary>
    /// Options for configuring <c>UseFastEndpointsWithConventions</c>.
    /// Controls which authentication schemes are applied globally to all authenticated endpoints.
    /// </summary>
    public sealed class FastEndpointsConventionOptions
    {
        /// <summary>
        /// Gets or sets the auth mode for cookie-based authentication (ASP.NET Core Identity).
        /// When <see cref="AuthMode.OptOut"/>, all authenticated endpoints accept cookie auth by default.
        /// When <see cref="AuthMode.OptIn"/> (default), endpoints must explicitly declare it.
        /// </summary>
        public AuthMode CookieAuthMode { get; set; } = AuthMode.OptIn;

        /// <summary>
        /// Gets or sets the auth mode for JWT Bearer authentication.
        /// When <see cref="AuthMode.OptOut"/>, all authenticated endpoints accept JWT auth by default.
        /// When <see cref="AuthMode.OptIn"/> (default), endpoints must explicitly declare it.
        /// </summary>
        public AuthMode JwtAuthMode { get; set; } = AuthMode.OptIn;
    }
}
