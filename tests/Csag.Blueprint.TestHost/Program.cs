using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.TestHost.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Middleware;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Bind and validate every Blueprint option section up front; validation failures stop the host
// before any service can observe half-configured options.
builder.Services.AddBlueprintDefaultValidatedOptions(builder.Configuration);

// Package-level services: server options, OIDC, FastEndpoints, Swagger, distributed cache,
// anti-forgery, and the claims-based tenant resolver.
builder.AddBlueprintServices();

// Host-level services: persistence, identity, session auth, policies, table views, localization,
// health checks, and startup orchestration (schema + seed data).
builder.AddTestHostServices();

var app = builder.Build();

// Audit.NET global settings: SQL Server data provider, EF entity tracking for the host's context,
// and enrichment from the HTTP context. Must run after Build so the service provider exists.
app.ConfigureBlueprintAuditLogging<TestDbContext, TestUser, TestRole>();

app.UseBlueprintSecurityHeaders();
app.UseBlueprintMiddleware();
app.UseMiddleware<HttpAuditMiddleware>();

// The runtime Swagger UI/JSON endpoints are gated behind configuration; only the Testing and
// Development settings enable them.
var securitySettings = app.Services.GetRequiredService<IOptions<SecuritySettings>>().Value;

app.UseFastEndpointsWithConventions(o =>
{
    // The package cannot know this host's namespace, so the [namespace] routing convention
    // requires the endpoint root explicitly.
    o.EndpointsBaseNamespace = "Csag.Blueprint.TestHost.Endpoints";
    o.CookieAuthMode = AuthMode.OptOut;
    o.EnableSwaggerUi = securitySettings.Swagger.Enabled;
});

app.MapTestHostHealthChecks();

await app.RunAsync();

// REMARK: Standard pattern making an entry-point marker class available to integration tests, so
// WebApplicationFactory-based fixtures can reference this assembly through a typed anchor.
// See: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
namespace Csag.Blueprint.TestHost
{
    /// <summary>
    /// Entry-point marker for WebApplicationFactory-based test fixtures.
    /// </summary>
    [SuppressMessage("SonarQube", "S2094", Justification = "Intentionally empty marker class anchoring the entry assembly for test fixtures.")]
    [SuppressMessage("SonarQube", "S2333", Justification = "Partial keeps the marker open for test-side extension, mirroring the documented WebApplicationFactory pattern.")]
    public partial class Program;
}
