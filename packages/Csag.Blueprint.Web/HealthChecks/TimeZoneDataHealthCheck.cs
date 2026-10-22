namespace Csag.Blueprint.Web.HealthChecks;

using Csag.Blueprint.Domain.Time;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies the host has time zone data, by resolving a well-known IANA zone.
/// </summary>
/// <remarks>
/// <see cref="TimeZoneInfo"/> reads IANA zones from the operating system: the <c>tzdata</c> package on Linux,
/// ICU on Windows. Slim container images (Alpine, chiseled) ship without it, and then every zone lookup fails
/// at the first request that needs one. Registered as a readiness check, this keeps such an image from
/// receiving traffic. Use <see cref="TimeZoneDataHealthCheckExtensions.AddBlueprintTimeZoneDataCheck"/>.
/// </remarks>
public sealed class TimeZoneDataHealthCheck : IHealthCheck
{
    /// <summary>
    /// The zone resolved when no other is given: one with daylight saving time, so a host that knows only UTC
    /// fails the check.
    /// </summary>
    public static readonly string DefaultProbeZoneId = "Europe/Zurich";

    private readonly string probeZoneId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneDataHealthCheck"/> class that resolves
    /// <see cref="DefaultProbeZoneId"/>.
    /// </summary>
    public TimeZoneDataHealthCheck()
        : this(DefaultProbeZoneId)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneDataHealthCheck"/> class that resolves the given zone.
    /// </summary>
    /// <param name="probeZoneId">The IANA id of the zone to resolve, such as the tenants' default zone.</param>
    public TimeZoneDataHealthCheck(string probeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(probeZoneId);

        this.probeZoneId = probeZoneId;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (ZonedTime.TryFindIanaTimeZone(this.probeZoneId, out _))
        {
            return Task.FromResult(HealthCheckResult.Healthy($"The time zone data resolves '{this.probeZoneId}'."));
        }

        return Task.FromResult(new HealthCheckResult(
            context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
            $"The time zone data cannot resolve the IANA zone '{this.probeZoneId}'. Install the tzdata package in the image."));
    }
}
