namespace Csag.Blueprint.Web.Options.Api.Security.HttpsRedirect
{
    /// <summary>
    /// HTTPS redirection configuration settings.
    /// </summary>
    public sealed class HttpsRedirectSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether HTTPS redirection is enabled.
        /// When true, all HTTP requests will be redirected to HTTPS.
        /// Recommended: false for local development, true for production (unless handled by load balancer).
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code to use for HTTPS redirection.
        /// - 301 (Moved Permanently): Permanent redirect, browsers will cache.
        /// - 307 (Temporary Redirect): Temporary redirect, preserves HTTP method.
        /// - 308 (Permanent Redirect): Permanent redirect, preserves HTTP method (recommended for APIs).
        /// </summary>
        public int RedirectStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the HTTPS port to redirect to.
        /// If null, uses the default HTTPS port (443) or the port configured in the application.
        /// Only set this if using a non-standard HTTPS port (e.g., 20021 in development).
        /// </summary>
        public int? HttpsPort { get; set; }
    }
}
