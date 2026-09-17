namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Web.Options.Api.Security;
    using Csag.Blueprint.Web.Options.Api.Security.Cors;
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
        /// Wildcard misconfigurations are rejected here rather than inside the policy callbacks,
        /// because those callbacks only run when <see cref="CorsOptions"/> is first resolved —
        /// which would let a broken policy lie dormant until an arbitrary later point in startup.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        /// <param name="securitySettings">The security settings.</param>
        /// <returns>The web application builder for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a policy mixes the wildcard origin "*" with explicit origins, or combines the
        /// wildcard origin with AllowCredentials.
        /// </exception>
        public static WebApplicationBuilder AddConfiguredCors(this WebApplicationBuilder builder, SecuritySettings securitySettings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(securitySettings);

            foreach (var (policyName, corsSettings) in securitySettings.CorsPolicies)
            {
                EnsureValidWildcardUsage(policyName, corsSettings);

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

        private static void EnsureValidWildcardUsage(string policyName, CorsSettings corsSettings)
        {
            var origins = SplitOrigins(corsSettings.AllowedOrigins);
            if (!origins.Contains("*", StringComparer.Ordinal))
            {
                return;
            }

            if (origins.Length > 1)
            {
                throw new InvalidOperationException(
                    $"CORS policy '{policyName}' mixes the wildcard origin '*' with explicit origins in AllowedOrigins. " +
                    "The wildcard only means 'any origin' when it stands alone; use '*' by itself or list explicit origins only.");
            }

            if (corsSettings.AllowCredentials)
            {
                throw new InvalidOperationException(
                    $"CORS policy '{policyName}' combines the wildcard origin '*' with AllowCredentials. " +
                    "The CORS protocol forbids credentials with any origin; list explicit origins instead.");
            }
        }

        private static string[] SplitOrigins(string? allowedOrigins)
        {
            return string.IsNullOrWhiteSpace(allowedOrigins)
                ? []
                : allowedOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static void ConfigureCorsOrigins(CorsPolicyBuilder policy, string? allowedOrigins)
        {
            var origins = SplitOrigins(allowedOrigins);
            if (origins.Length == 0)
            {
                return;
            }

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
