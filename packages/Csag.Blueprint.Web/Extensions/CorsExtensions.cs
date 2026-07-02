namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// Extension methods for configuring CORS on WebApplication.
    /// </summary>
    public static class CorsExtensions
    {
        /// <summary>
        /// Applies the default CORS policy if configured in SecuritySettings.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication UseCorsIfConfigured(this WebApplication app, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(securitySettings);

            var policyName = securitySettings.DefaultCorsPolicy;
            if (!string.IsNullOrWhiteSpace(policyName))
            {
                app.UseCors(policyName);
            }

            return app;
        }
    }
}
