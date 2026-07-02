namespace Csag.Blueprint.Web.Options.Api.Security.Csrf
{
    /// <summary>
    /// Configuration settings for CSRF (Cross-Site Request Forgery) protection.
    /// Nested under Blueprint:Security:Csrf in appsettings.json.
    /// </summary>
    public sealed class CsrfSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether CSRF protection is enabled.
        /// When disabled, the middleware skips token distribution and validation.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the name of the HTTP request header used to submit the CSRF token.
        /// The frontend reads the request token cookie and sends its value in this header.
        /// </summary>
        public string HeaderName { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name of the HttpOnly cookie used by the antiforgery system for validation.
        /// This cookie is managed automatically by ASP.NET Core and is not readable by JavaScript.
        /// </summary>
        public string CookieName { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name of the non-HttpOnly cookie that carries the request token.
        /// JavaScript on the same origin can read this cookie value and send it as the request header.
        /// </summary>
        public string RequestTokenCookieName { get; set; } = null!;
    }
}
