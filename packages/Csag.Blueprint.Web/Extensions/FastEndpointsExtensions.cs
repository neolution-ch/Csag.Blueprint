namespace Csag.Blueprint.Web.Extensions
{
    using Csag.Blueprint.Application.Json;
    using Csag.Blueprint.Infrastructure.Authentication;
    using FastEndpoints;
    using FastEndpoints.Swagger;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Primitives;

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
            // Apply the shared Blueprint JSON conventions (camelCase property names,
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
        /// Both default to <see cref="AuthMode.OptIn"/> when not configured. Every authenticated endpoint that
        /// declares no scheme of its own gets exactly one: the session cookie, <see cref="BlueprintAuthenticationSchemes.Bearer"/>,
        /// or, with both modes <see cref="AuthMode.OptOut"/>, <see cref="BlueprintAuthenticationSchemes.SessionOrBearer"/>,
        /// which picks one of the two per request.
        /// </param>
        public static WebApplication UseFastEndpointsWithConventions(this WebApplication app, Action<FastEndpointsConventionOptions>? configure = null)
        {
            var options = new FastEndpointsConventionOptions();
            configure?.Invoke(options);

            // Fail fast at startup: without the host's endpoint root namespace the [namespace] route
            // placeholder cannot be resolved, and the package must not guess it.
            if (string.IsNullOrWhiteSpace(options.EndpointsBaseNamespace))
            {
                throw new InvalidOperationException(
                    $"{nameof(FastEndpointsConventionOptions)}.{nameof(FastEndpointsConventionOptions.EndpointsBaseNamespace)} must be set to the root " +
                    "namespace of the application's endpoint classes (e.g. \"MyCompany.Api.Endpoints\").");
            }

            var endpointsBaseNamespace = options.EndpointsBaseNamespace;

            // One scheme, never a list: each request is authenticated by exactly one scheme.
            var defaultScheme = (options.CookieAuthMode, options.JwtAuthMode) switch
            {
                (AuthMode.OptOut, AuthMode.OptOut) => BlueprintAuthenticationSchemes.SessionOrBearer,
                (AuthMode.OptOut, _) => IdentityConstants.ApplicationScheme,
                (_, AuthMode.OptOut) => BlueprintAuthenticationSchemes.Bearer,
                _ => null,
            };

            app
                .UseFastEndpoints(x =>
                {
                    x.Endpoints.RoutePrefix = "api";
                    x.Endpoints.ShortNames = true;
                    x.Errors.UseProblemDetails(o => o.AllowDuplicateErrors = true);
                    x.Endpoints.Configurator = ep =>
                    {
                        ep.ApplyConventions(endpointsBaseNamespace);

                        // Apply the opt-out scheme to every authenticated endpoint that declares none.
                        // AuthSchemes() adds to the schemes an endpoint already declared, so an endpoint
                        // with its own is left alone, and anonymous endpoints get none.
                        if (defaultScheme is not null && ep.AnonymousVerbs is null && ep.AuthSchemeNames is not { Count: > 0 })
                        {
                            ep.AuthSchemes(defaultScheme);
                        }
                    };

                    // Map malformed/truncated multipart form bodies to 400 Bad Request instead of
                    // letting IOException / InvalidDataException escape as 500 Internal Server Error.
                    // Without this, security scanners (e.g. OWASP ZAP) flag the 500 as a SQL injection.
                    x.Binding.FormExceptionTransformer =
                        _ => new FluentValidation.Results.ValidationFailure("_", "The request body was malformed or contained invalid multipart data.");

                    // Query, route, form and header values bypass System.Text.Json, and FastEndpoints' own
                    // DateTime parsing accepts offsets (converting to the server's zone), fractions and
                    // culture formats. These parsers accept exactly the local readings a JSON body does.
                    x.Binding.ValueParserFor<DateTime>(ParseLocalReading);
                    x.Binding.ValueParserFor<DateTime?>(ParseOptionalLocalReading);
                });

            // Serve the Swagger UI HTML and the runtime Swagger JSON document only when enabled.
            // Because the JSON API has no CSP by design, the served Swagger UI HTML is unprotected, so
            // callers turn this off in Production via configuration. Gating only this middleware keeps the
            // Swagger document registered in DI, which means the build-time OpenAPI export stays unaffected.
            if (options.EnableSwaggerUi)
            {
                app.UseSwaggerGen();
            }

            return app;
        }

        private static ParseResult ParseLocalReading(StringValues input)
        {
            return LocalDateTimeFormat.TryParse(input.ToString(), out var reading)
                ? new ParseResult(true, reading)
                : new ParseResult(false, null);
        }

        private static ParseResult ParseOptionalLocalReading(StringValues input)
        {
            return StringValues.IsNullOrEmpty(input) ? new ParseResult(true, null) : ParseLocalReading(input);
        }
    }
}
