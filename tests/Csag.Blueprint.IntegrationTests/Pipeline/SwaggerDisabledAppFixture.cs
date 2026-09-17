namespace Csag.Blueprint.IntegrationTests.Pipeline;

using Csag.Blueprint.Web.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// An <see cref="AppFixture"/> variant that force-disables the Swagger toggle
/// (Blueprint:Security:Swagger:Enabled = false) even though the Testing environment enables it,
/// so <see cref="SwaggerDisabledEndpointTests"/> can exercise the disabled pipeline branch against
/// a live host. Setup is trimmed to the startup readiness check only — this second host just needs
/// to boot; it needs no authenticated clients or database snapshot.
/// </summary>
public sealed class SwaggerDisabledAppFixture : AppFixture
{
    /// <inheritdoc />
    protected override void ConfigureApp(IWebHostBuilder a)
    {
        base.ConfigureApp(a);

        // A higher-precedence override of appsettings.Testing.json (which turns Swagger on), the
        // same way the base fixture overrides the connection string. Drives EnableSwaggerUi = false
        // in the host's FastEndpoints setup.
        a.UseSetting("Blueprint:Security:Swagger:Enabled", "false");
    }

    /// <inheritdoc />
    protected override ValueTask SetupAsync()
    {
        // Only a booted host is needed for the disabled-Swagger assertions; skip the base client
        // logins and snapshot to keep this extra host cheap.
        var readyCheck = this.Services.GetRequiredService<StartupCompletedHealthCheck>();
        if (!readyCheck.StartupCompleted)
        {
            throw new InvalidOperationException(
                "TestHostStartupService did not complete successfully for SwaggerDisabledAppFixture. "
                + "The host likely failed while creating the schema or seeding data. "
                + "Check the test output for error logs.");
        }

        return ValueTask.CompletedTask;
    }
}
