namespace Csag.Blueprint.Web.Options.Api.Security
{
    using Csag.Blueprint.Web.Options.Api.Security.Cors;
    using Csag.Blueprint.Web.Options.Api.Security.Csrf;
    using Csag.Blueprint.Web.Options.Api.Security.HttpsRedirect;
    using Csag.Blueprint.Web.Options.Api.Security.Jwt;
    using Csag.Blueprint.Web.Options.Api.Security.OAuth;
    using Csag.Blueprint.Web.Options.Api.Security.Password;
    using Csag.Blueprint.Web.Options.Api.Security.PasswordReset;
    using Csag.Blueprint.Web.Options.Api.Security.RequestLimits;
    using Csag.Blueprint.Web.Options.Api.Security.SecurityHeaders;
    using Csag.Blueprint.Web.Options.Api.Security.ServiceAccountLockout;
    using Csag.Blueprint.Web.Options.Api.Security.Swagger;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Security configuration settings for the API.
    /// Includes CORS, HTTPS redirection, HTTP security headers, and OAuth external authentication.
    /// </summary>
    public sealed class SecuritySettings
    {
        /// <summary>
        /// Gets or sets the name of the default CORS policy to apply globally.
        /// If null or empty, no global CORS policy is applied (policies must be specified per-endpoint).
        /// The policy name must exist in the CorsPolicies dictionary.
        /// </summary>
        public string? DefaultCorsPolicy { get; set; }

        /// <summary>
        /// Gets or sets the CORS (Cross-Origin Resource Sharing) policies.
        /// Each key is a policy name, and the value contains the policy configuration.
        /// Multiple policies allow different CORS settings for different client types (e.g., "WebApp", "MobileApp", "ThirdParty").
        /// </summary>
        public Dictionary<string, CorsSettings> CorsPolicies { get; set; } = new();

        /// <summary>
        /// Gets or sets the HTTPS redirection settings.
        /// Controls whether and how HTTP requests are redirected to HTTPS.
        /// Note: In production environments with load balancers/reverse proxies, HTTPS termination is often handled at that level.
        /// Disable these settings if your infrastructure handles HTTPS redirection externally.
        /// </summary>
        public HttpsRedirectSettings HttpsRedirect { get; set; } = new();

        /// <summary>
        /// Gets or sets the security headers settings.
        /// Controls HTTP Strict Transport Security (HSTS) and other security-related response headers.
        /// Note: In production environments with load balancers/reverse proxies, security headers are often configured at that level.
        /// Disable these settings if your infrastructure handles security headers externally to avoid duplication.
        /// </summary>
        public SecurityHeadersSettings SecurityHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the password policy settings.
        /// Configures password requirements for user registration and authentication.
        /// These settings are applied to ASP.NET Core Identity framework.
        /// </summary>
        public PasswordSettings PasswordSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the OAuth external authentication provider settings.
        /// Configures integration with third-party identity providers like Google, Microsoft, and GitHub.
        /// Enables users to sign in using their existing accounts from external providers.
        /// </summary>
        public OAuthSettings OAuth { get; set; } = new();

        /// <summary>
        /// Gets or sets the password reset settings.
        /// Configures the frontend URL for reset links and the token lifetime.
        /// </summary>
        public PasswordResetSettings PasswordResetSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the JWT authentication settings for service accounts.
        /// Configures signing key, issuer, audience, and token expiration for machine-to-machine authentication.
        /// </summary>
        public JwtSettings Jwt { get; set; } = new();

        /// <summary>
        /// Gets or sets the service-account token endpoint lockout settings.
        /// Configures the failed-attempt threshold and lockout duration for service-account credential
        /// verification, mirroring the semantics of ASP.NET Core Identity lockout for interactive users.
        /// </summary>
        public ServiceAccountLockoutSettings ServiceAccountLockout { get; set; } = new();

        /// <summary>
        /// Gets or sets the multi-factor authentication (MFA) settings.
        /// Controls whether TOTP-based MFA is supported, whether it is mandatory for all users,
        /// and which external login providers bypass the MFA challenge.
        /// </summary>
        public MfaSettings Mfa { get; set; } = new();

        /// <summary>
        /// Gets or sets the CSRF (Cross-Site Request Forgery) protection settings.
        /// Configures token distribution and validation for cookie-authenticated requests.
        /// </summary>
        public CsrfSettings Csrf { get; set; } = new();

        /// <summary>
        /// Gets or sets the request size limit settings.
        /// Centrally caps the request body size (Kestrel) and the multipart form body size
        /// to protect the API against oversized requests and resource exhaustion.
        /// </summary>
        public RequestLimitsSettings RequestLimits { get; set; } = new();

        /// <summary>
        /// Gets or sets the cookie secure policy for the Identity session cookie.
        /// Use <see cref="CookieSecurePolicy.Always"/> in production to ensure the cookie is only sent over HTTPS.
        /// Use <see cref="CookieSecurePolicy.SameAsRequest"/> in development or behind HTTP-only proxies.
        /// </summary>
        public CookieSecurePolicy CookieSecurePolicy { get; set; }

        /// <summary>
        /// Gets or sets the number of hours before a user session expires.
        /// This single value drives both the authentication cookie <c>ExpireTimeSpan</c> and the sign-in
        /// ticket <c>ExpiresUtc</c>, so the cookie and ticket lifetimes can never drift.
        /// The session cookie is always persistent across browser restarts.
        /// Because sliding expiration is enabled, this is an <b>idle (inactivity) timeout</b>: the session
        /// is extended by this value on activity once more than half of the window has elapsed, and expires
        /// only after this many hours of inactivity. There is no separate absolute (maximum) lifetime cap.
        /// Defaults to 168 hours (7 days).
        /// </summary>
        public int SessionExpirationHours { get; set; }

        /// <summary>
        /// Gets or sets the Swagger UI settings.
        /// Controls whether the runtime Swagger UI HTML and Swagger JSON endpoints are served.
        /// The JSON API has no CSP by design, so the served Swagger UI HTML is unprotected.
        /// Defaults to disabled (off) so the exposed surface is never served unless an environment
        /// explicitly opts in; it is enabled only in the Development and Testing appsettings, while
        /// Staging and Production inherit the disabled default. Remains toggleable via configuration.
        /// </summary>
        public SwaggerSettings Swagger { get; set; } = new();
    }
}
