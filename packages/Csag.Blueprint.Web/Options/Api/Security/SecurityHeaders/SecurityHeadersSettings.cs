namespace Csag.Blueprint.Web.Options.Api.Security.SecurityHeaders
{
    /// <summary>
    /// Security headers configuration settings.
    /// </summary>
    public sealed class SecurityHeadersSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether HSTS (HTTP Strict Transport Security) is enabled.
        /// When true, the Strict-Transport-Security header is sent with preload-compatible settings:
        /// - max-age=31536000 (1 year)
        /// - includeSubDomains
        /// - preload
        /// This tells browsers to always use HTTPS for this domain, even on first visit.
        /// WARNING: Do not enable in development without proper HTTPS certificates.
        /// Recommended: false for local development, true for production (unless handled by load balancer).
        /// </summary>
        public bool EnableHsts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether additional security headers should be sent.
        /// When true, the following headers are added to all responses:
        /// - X-Content-Type-Options: nosniff (prevents MIME type sniffing)
        /// - X-Frame-Options: DENY (prevents clickjacking)
        /// - Referrer-Policy: strict-origin-when-cross-origin (controls referrer information)
        /// - Permissions-Policy: geolocation=(), microphone=(), camera=() (disables specific browser features)
        /// Recommended: false for local development, true for production (unless handled by load balancer).
        /// </summary>
        public bool EnableSecurityHeaders { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether to remove server identity headers that expose implementation details.
        /// When true, the following headers are removed from responses:
        /// - Server (reveals web server: Kestrel, IIS, etc.)
        /// - X-Powered-By (reveals ASP.NET/IIS)
        /// - X-AspNet-Version (reveals ASP.NET version)
        /// - X-AspNetMvc-Version (reveals MVC version)
        /// This prevents information disclosure that could help attackers target known vulnerabilities.
        /// Recommended: false for local development, true for production.
        /// </summary>
        public bool RemoveServerIdentityHeaders { get; set; }
    }
}
