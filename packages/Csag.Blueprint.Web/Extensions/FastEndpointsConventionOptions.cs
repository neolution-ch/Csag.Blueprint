namespace Csag.Blueprint.Web.Extensions
{
    /// <summary>
    /// Options for configuring <c>UseFastEndpointsWithConventions</c>.
    /// Controls which authentication schemes are applied globally to all authenticated endpoints.
    /// </summary>
    public sealed class FastEndpointsConventionOptions
    {
        /// <summary>
        /// Gets or sets the root namespace of the application's endpoint classes (e.g.
        /// <c>"MyCompany.Api.Endpoints"</c>). The <c>[namespace]</c> route placeholder resolves to the
        /// kebab-cased first namespace segment below this root. <b>Required</b> — the package cannot
        /// know the host's namespace, so there is no default and startup fails when it is unset.
        /// </summary>
        public string? EndpointsBaseNamespace { get; set; }

        /// <summary>
        /// Gets or sets the auth mode for cookie-based authentication (ASP.NET Core Identity).
        /// When <see cref="AuthMode.OptOut"/>, all authenticated endpoints accept cookie auth by default.
        /// When <see cref="AuthMode.OptIn"/> (default), endpoints must explicitly declare it.
        /// </summary>
        public AuthMode CookieAuthMode { get; set; } = AuthMode.OptIn;

        /// <summary>
        /// Gets or sets the auth mode for JWT Bearer authentication.
        /// When <see cref="AuthMode.OptOut"/>, all authenticated endpoints accept JWT auth by default.
        /// When <see cref="AuthMode.OptIn"/> (default), endpoints must explicitly declare it.
        /// </summary>
        public AuthMode JwtAuthMode { get; set; } = AuthMode.OptIn;

        /// <summary>
        /// Gets or sets a value indicating whether the runtime Swagger UI and Swagger JSON
        /// endpoints (<c>UseSwaggerGen()</c>) are served. The caller is expected to set this
        /// explicitly (see <c>Program.cs</c>, which derives it from configuration).
        /// When <c>true</c>, the Swagger UI HTML and the runtime Swagger JSON document are exposed.
        /// When <c>false</c> (the default), <c>UseSwaggerGen()</c> is not added to the pipeline, so those
        /// routes return 404. This does not affect the build-time OpenAPI export used for client generation.
        /// </summary>
        public bool EnableSwaggerUi { get; set; }
    }
}
