namespace Csag.Blueprint.Web.UnitTests.HealthChecks;

using Csag.Blueprint.Web.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// Unit tests for <see cref="TimeZoneDataHealthCheck"/> and its registration. A host without time zone
/// data cannot resolve any IANA zone, which is what probing a zone that does not exist simulates.
/// </summary>
public sealed class TimeZoneDataHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenBothZonesResolve_ReportsHealthy()
    {
        var result = await new TimeZoneDataHealthCheck("Europe/Zurich", "America/New_York")
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull().ShouldContain("Europe/Zurich");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLegacyNameDoesNotResolve_ReportsDegraded()
    {
        // A legacy probe no tz database has stands in for a host without tzdata-legacy.
        var result = await new TimeZoneDataHealthCheck("Europe/Zurich", "Mars/Olympus_Mons")
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldNotBeNull().ShouldContain("tzdata-legacy");
    }

    [Fact]
    public async Task CheckHealthAsync_WithDefaults_IsNotUnhealthyOnHostWithTimeZoneData()
    {
        // Degraded on a host without the legacy links, such as GitHub's Ubuntu runner; Healthy elsewhere.
        var result = await new TimeZoneDataHealthCheck().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldNotBe(HealthStatus.Unhealthy);
    }

    [Theory]
    [InlineData("Europe/Atlantis")]
    [InlineData("W. Europe Standard Time")]
    public async Task CheckHealthAsync_WhenZoneDoesNotResolve_ReportsUnhealthy(string probeZoneId)
    {
        var result = await new TimeZoneDataHealthCheck(probeZoneId).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("tzdata");
    }

    [Fact]
    public void AddBlueprintTimeZoneDataCheck_RegistersReadinessCheck()
    {
        var services = new ServiceCollection();

        services.AddHealthChecks().AddBlueprintTimeZoneDataCheck();

        using var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ShouldHaveSingleItem();
        registration.Name.ShouldBe("time_zone_data_check");
        registration.Tags.ShouldBe(["ready"]);
        registration.FailureStatus.ShouldBe(HealthStatus.Unhealthy);
        registration.Factory(provider).ShouldBeOfType<TimeZoneDataHealthCheck>();
    }

    [Fact]
    public async Task AddBlueprintTimeZoneDataCheck_WithOverrides_UsesThem()
    {
        var services = new ServiceCollection();

        services.AddHealthChecks().AddBlueprintTimeZoneDataCheck("tz", ["startup"], "Europe/Atlantis");

        using var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ShouldHaveSingleItem();
        registration.Name.ShouldBe("tz");
        registration.Tags.ShouldBe(["startup"]);
        var result = await registration.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = registration }, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
