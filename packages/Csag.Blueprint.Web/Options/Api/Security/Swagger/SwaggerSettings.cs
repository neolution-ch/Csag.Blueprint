namespace Csag.Blueprint.Web.Options.Api.Security.Swagger
{
    /// <summary>
    /// Swagger/OpenAPI UI configuration settings.
    /// Controls whether the runtime Swagger endpoints (the Swagger UI HTML and the
    /// <c>/swagger/{documentName}/swagger.json</c> document) are served by the application.
    /// </summary>
    public sealed class SwaggerSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the runtime Swagger UI and Swagger JSON endpoints are served.
        /// When <c>true</c>, <c>UseSwaggerGen()</c> is added to the pipeline and the Swagger UI is reachable.
        /// When <c>false</c>, neither the Swagger UI HTML nor the runtime Swagger JSON document is exposed
        /// (routes return 404). This does NOT affect the build-time OpenAPI export used for client generation,
        /// which is produced via the <c>--exportswaggerjson</c> flow from the Swagger document registered in DI.
        /// Defaults to <c>false</c> (off) so Swagger is never served unless an environment explicitly opts in.
        /// Enabled only in Development and Testing (see their appsettings); Staging and Production inherit the
        /// disabled default. Remains fully toggleable via configuration (e.g. an environment variable).
        /// </summary>
        public bool Enabled { get; set; }
    }
}
