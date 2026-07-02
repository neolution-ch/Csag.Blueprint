namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Cors.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for configuring CORS services on WebApplicationBuilder.
    /// </summary>
    public static class CorsBuilderExtensions
    {
        /// <summary>
        /// Configures CORS policies from SecuritySettings configuration.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        public static WebApplicationBuilder AddConfiguredCors(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            foreach (var (policyName, corsSettings) in securitySettings.CorsPolicies)
            {
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy(policyName, policy =>
                    {
                        ConfigureCorsOrigins(policy, corsSettings.AllowedOrigins);
                        ConfigureCorsMethods(policy, corsSettings.AllowedMethods);
                        ConfigureCorsHeaders(policy, corsSettings.AllowedHeaders);
                        ConfigureCorsExposedHeaders(policy, corsSettings.ExposedHeaders);

                        if (corsSettings.AllowCredentials)
                        {
                            policy.AllowCredentials();
                        }

                        if (corsSettings.PreflightMaxAgeSeconds > 0)
                        {
                            policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.PreflightMaxAgeSeconds));
                        }
                    });
                });
            }

            return builder;
        }

        private static void ConfigureCorsOrigins(CorsPolicyBuilder policy, string? allowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(allowedOrigins))
            {
                return;
            }

            var origins = allowedOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (origins.Length == 1 && origins[0] == "*")
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(origins);
            }
        }

        private static void ConfigureCorsMethods(CorsPolicyBuilder policy, string? allowedMethods)
        {
            if (string.IsNullOrWhiteSpace(allowedMethods))
            {
                policy.AllowAnyMethod();
                return;
            }

            var methods = allowedMethods.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithMethods(methods);
        }

        private static void ConfigureCorsHeaders(CorsPolicyBuilder policy, string? allowedHeaders)
        {
            if (string.IsNullOrWhiteSpace(allowedHeaders))
            {
                policy.AllowAnyHeader();
                return;
            }

            var headers = allowedHeaders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithHeaders(headers);
        }

        private static void ConfigureCorsExposedHeaders(CorsPolicyBuilder policy, string? exposedHeaders)
        {
            if (string.IsNullOrWhiteSpace(exposedHeaders))
            {
                return;
            }

            var exposedHeadersList = exposedHeaders.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithExposedHeaders(exposedHeadersList);
        }
    }
}
