namespace Csag.Blueprint.Web.Options.Api.Security.Cors
{
    /// <summary>
    /// CORS (Cross-Origin Resource Sharing) configuration settings.
    /// </summary>
    public sealed class CorsSettings
    {
        /// <summary>
        /// Gets or sets the allowed origins as a semicolon-delimited string (e.g., "http://localhost:3000;https://app.example.com").
        /// Do not include trailing slashes in URLs.
        /// </summary>
        public string? AllowedOrigins { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether credentials (cookies, authorization headers) are allowed in cross-origin requests.
        /// When true, AllowedOrigins must be specified explicitly (cannot use wildcard "*").
        /// </summary>
        public bool AllowCredentials { get; set; }

        /// <summary>
        /// Gets or sets the allowed HTTP methods as a semicolon-delimited string (e.g., "GET;POST;PUT;DELETE").
        /// If null or empty, defaults to allowing all methods.
        /// </summary>
        public string? AllowedMethods { get; set; }

        /// <summary>
        /// Gets or sets the allowed request headers as a semicolon-delimited string (e.g., "Content-Type;Authorization").
        /// If null or empty, defaults to allowing all headers.
        /// </summary>
        public string? AllowedHeaders { get; set; }

        /// <summary>
        /// Gets or sets the response headers to expose to the client as a semicolon-delimited string (e.g., "X-Custom-Header;X-Another-Header").
        /// By default, only simple response headers are exposed.
        /// </summary>
        public string? ExposedHeaders { get; set; }

        /// <summary>
        /// Gets or sets the maximum age (in seconds) that preflight request results can be cached by the browser.
        /// Recommended: 600 seconds (10 minutes) for development, 3600 seconds (1 hour) or more for production.
        /// This reduces the number of OPTIONS requests sent by the browser.
        /// </summary>
        public int PreflightMaxAgeSeconds { get; set; }
    }
}
