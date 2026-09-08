namespace Csag.Blueprint.TestHost.Extensions;

using Csag.Blueprint.Web.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Registers and maps the host's health probes: a trivial liveness check and a readiness check
/// gated on the one-shot startup flag flipped by <c>TestHostStartupService</c> once the schema is
/// created and the seed data is in place.
/// </summary>
public static class TestHostHealthCheckExtensions
{
    /// <summary>
    /// Adds the liveness and readiness checks. <see cref="StartupCompletedHealthCheck"/> is
    /// registered as a singleton so the startup service and tests can flip the same instance's gate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<StartupCompletedHealthCheck>();

        services.AddHealthChecks()
            .AddCheck("live_check", () => HealthCheckResult.Healthy("The host process is responsive."), tags: ["live"])
            .AddCheck<StartupCompletedHealthCheck>("ready_check", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps <c>/health/livez</c> and <c>/health/readyz</c>, selecting checks by tag.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapTestHostHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/livez", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });

        app.MapHealthChecks("/health/readyz", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        return app;
    }
}
