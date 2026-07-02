namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpsPolicy;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for configuring security-related services on WebApplicationBuilder.
    /// </summary>
    public static class SecurityBuilderExtensions
    {
        /// <summary>
        /// Configures HTTPS redirection options if enabled in SecuritySettings.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder AddHttpsRedirection(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.HttpsRedirect.Enabled)
            {
                builder.Services.Configure<HttpsRedirectionOptions>(options =>
                {
                    options.RedirectStatusCode = securitySettings.HttpsRedirect.RedirectStatusCode;
                    if (securitySettings.HttpsRedirect.HttpsPort.HasValue)
                    {
                        options.HttpsPort = securitySettings.HttpsRedirect.HttpsPort.Value;
                    }
                });
            }

            return builder;
        }

        /// <summary>
        /// Configures HSTS (HTTP Strict Transport Security) options if enabled in SecuritySettings.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder AddHsts(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.SecurityHeaders.EnableHsts)
            {
                builder.Services.Configure<HstsOptions>(options =>
                {
                    options.MaxAge = TimeSpan.FromDays(365);
                    options.IncludeSubDomains = true;
                    options.Preload = true;
                });
            }

            return builder;
        }

        /// <summary>
        /// Configures Kestrel server options to remove the Server header if enabled in SecuritySettings.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder ConfigureKestrelServerOptions(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            if (securitySettings.SecurityHeaders.RemoveServerIdentityHeaders)
            {
                builder.Services.Configure<KestrelServerOptions>(options =>
                {
                    options.AddServerHeader = false;
                });
            }

            return builder;
        }
    }
}
