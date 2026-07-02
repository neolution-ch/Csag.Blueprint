namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Extension methods for configuring security-related middleware on WebApplication.
    /// </summary>
    public static class SecurityExtensions
    {
        /// <summary>
        /// Adds HTTPS redirection middleware if enabled in SecuritySettings.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseHttpsRedirectionIfEnabled(this WebApplication app, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.HttpsRedirect.Enabled)
            {
                app.UseHttpsRedirection();
            }

            return app;
        }

        /// <summary>
        /// Adds HSTS middleware if enabled in SecuritySettings (production only).
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseHstsIfEnabled(this WebApplication app, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.SecurityHeaders.EnableHsts && !app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            return app;
        }

        /// <summary>
        /// Adds custom security headers middleware if enabled in SecuritySettings.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseSecurityHeaders(this WebApplication app, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.SecurityHeaders.EnableSecurityHeaders)
            {
                app.Use(async (context, next) =>
                {
                    // X-Content-Type-Options: Prevents MIME type sniffing
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

                    // X-Frame-Options: Prevents clickjacking attacks
                    context.Response.Headers["X-Frame-Options"] = "DENY";

                    // Referrer-Policy: Controls referrer information leakage
                    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                    // Permissions-Policy: Disables specific browser features (geolocation, microphone, camera)
                    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

                    await next();
                });
            }

            return app;
        }

        /// <summary>
        /// Removes server identity headers that expose implementation details if enabled in SecuritySettings.
        /// Removes headers like Server, X-Powered-By, X-AspNet-Version, and X-AspNetMvc-Version.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseServerIdentityHeaderRemoval(this WebApplication app, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.SecurityHeaders.RemoveServerIdentityHeaders)
            {
                app.Use(async (context, next) =>
                {
                    // Register callback to remove headers before response is sent
                    context.Response.OnStarting(() =>
                    {
                        // Remove headers that disclose server/framework information
                        context.Response.Headers.Remove("Server");
                        context.Response.Headers.Remove("X-Powered-By");
                        context.Response.Headers.Remove("X-AspNet-Version");
                        context.Response.Headers.Remove("X-AspNetMvc-Version");
                        return Task.CompletedTask;
                    });

                    await next();
                });
            }

            return app;
        }
    }
}
