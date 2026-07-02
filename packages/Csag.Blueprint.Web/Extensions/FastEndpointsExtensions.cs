namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Application.Json;
    using FastEndpoints;
    using FastEndpoints.Swagger;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for configuring FastEndpoints and Swagger on WebApplication.
    /// </summary>
    public static class FastEndpointsExtensions
    {
        /// <summary>
        /// Adds FastEndpoints with JSON serialization configuration.
        /// Configures JSON serialization to use string representation for enums.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFastEndpointsWithConfiguration(this IServiceCollection services)
        {
            // Apply the blueprint's shared JSON conventions (camelCase property names,
            // string enums). Same helper is used by persistence layers so the on-the-wire
            // and at-rest JSON shapes stay aligned.
            services.ConfigureHttpJsonOptions(options => BlueprintJsonOptions.Configure(options.SerializerOptions));

            services.AddFastEndpoints();

            return services;
        }

        /// <summary>
        /// Configures FastEndpoints with conventional routing and naming, and Swagger documentation.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="configure">
        /// Optional action to configure authentication scheme defaults.
        /// Use <see cref="FastEndpointsConventionOptions.CookieAuthMode"/> and
        /// <see cref="FastEndpointsConventionOptions.JwtAuthMode"/> to set each scheme to
        /// <see cref="AuthMode.OptOut"/> (applied globally) or <see cref="AuthMode.OptIn"/> (per-endpoint).
        /// Both default to <see cref="AuthMode.OptIn"/> when not configured.
        /// </param>
        public static WebApplication UseFastEndpointsWithConventions(this WebApplication app, Action<FastEndpointsConventionOptions>? configure = null)
        {
            var options = new FastEndpointsConventionOptions();
            configure?.Invoke(options);

            var schemes = new List<string>();
            if (options.CookieAuthMode == AuthMode.OptOut)
            {
                schemes.Add(IdentityConstants.ApplicationScheme);
            }

            if (options.JwtAuthMode == AuthMode.OptOut)
            {
                schemes.Add("Bearer");
            }

            app
                .UseFastEndpoints(x =>
                {
                    x.Endpoints.RoutePrefix = "api";
                    x.Endpoints.ShortNames = true;
                    x.Errors.UseProblemDetails(o => o.AllowDuplicateErrors = true);
                    x.Endpoints.Configurator = ep =>
                    {
                        ep.ApplyConventions();

                        // Apply opt-out schemes to all authenticated endpoints.
                        // AuthSchemes() has compounding behavior in the Configurator, so skip
                        // anonymous endpoints to avoid adding unwanted schemes.
                        if (schemes.Count > 0 && ep.AnonymousVerbs is null)
                        {
                            ep.AuthSchemes([.. schemes]);
                        }
                    };

                    // Map malformed/truncated multipart form bodies to 400 Bad Request instead of
                    // letting IOException / InvalidDataException escape as 500 Internal Server Error.
                    // Without this, security scanners (e.g. OWASP ZAP) flag the 500 as a SQL injection.
                    x.Binding.FormExceptionTransformer =
                        _ => new FluentValidation.Results.ValidationFailure("_", "The request body was malformed or contained invalid multipart data.");
                })
                .UseSwaggerGen();

            return app;
        }
    }
}
