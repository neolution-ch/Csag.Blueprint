namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.HttpsPolicy;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for configuring security-related services on WebApplicationBuilder.
    /// </summary>
    public static class SecurityBuilderExtensions
    {
        private const long BytesPerMegabyte = 1024 * 1024;

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
        /// Configures Kestrel server options: removes the Server header if enabled in SecuritySettings
        /// and caps the maximum request body size from RequestLimits settings.
        /// Oversized non-form bodies are rejected with 413 Payload Too Large; on multipart/form-data
        /// endpoints the rejection surfaces during form binding and is returned as a 400 Bad Request
        /// via the FastEndpoints FormExceptionTransformer.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder ConfigureKestrelServerOptions(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                if (securitySettings.SecurityHeaders.RemoveServerIdentityHeaders)
                {
                    options.AddServerHeader = false;
                }

                options.Limits.MaxRequestBodySize = securitySettings.RequestLimits.MaxRequestBodySizeMegabytes * BytesPerMegabyte;
            });

            return builder;
        }

        /// <summary>
        /// Configures form options to cap the length of each multipart section (e.g. each uploaded file)
        /// from RequestLimits settings. Sections exceeding the cap fail during form binding;
        /// FastEndpoints maps the failure to a 400 Bad Request via its FormExceptionTransformer.
        /// The total multipart request body is bounded by Kestrel's MaxRequestBodySize.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder ConfigureFormOptions(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = securitySettings.RequestLimits.MultipartBodyLengthLimitMegabytes * BytesPerMegabyte;
            });

            return builder;
        }
    }
}
