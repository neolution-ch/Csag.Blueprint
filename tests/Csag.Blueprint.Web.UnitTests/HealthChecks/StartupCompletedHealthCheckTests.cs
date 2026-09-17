namespace Csag.Blueprint.Web.UnitTests.HealthChecks;

using Csag.Blueprint.Web.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Unit tests for the reusable startup gate <see cref="StartupCompletedHealthCheck"/>. The gate represents
/// "one-shot startup orchestration completed" and is typically combined with application-level checks
/// (e.g. a database-ready probe) on the readiness endpoint.
/// </summary>
public sealed class StartupCompletedHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_BeforeStartupCompletes_ReportsUnhealthy()
    {
        // Arrange
        var sut = new StartupCompletedHealthCheck { StartupCompleted = false };

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert — not ready until the host's one-shot startup work (migrations, seeding, cache verification) has finished
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterStartupCompletes_ReportsHealthy()
    {
        // Arrange
        var sut = new StartupCompletedHealthCheck { StartupCompleted = true };

        // Act
        var result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
