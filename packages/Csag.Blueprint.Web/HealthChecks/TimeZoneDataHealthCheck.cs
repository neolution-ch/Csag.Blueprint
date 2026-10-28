namespace Csag.Blueprint.Web.HealthChecks;

using Csag.Blueprint.Domain.Time;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies the host has time zone data, by resolving a well-known IANA zone, and that the
/// data includes the legacy names browsers still send, by resolving one of them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TimeZoneInfo"/> reads IANA zones from the operating system: the <c>tzdata</c> package on Linux,
/// ICU on Windows. Slim container images (Alpine, chiseled) ship without it, and then every zone lookup fails
/// at the first request that needs one. Registered as a readiness check, this keeps such an image from
/// receiving traffic: the check is <see cref="HealthStatus.Unhealthy"/>.
/// </para>
/// <para>
/// Debian and Ubuntu ship the tz database's backward links (<c>Asia/Calcutta</c>, <c>Asia/Saigon</c>,
/// <c>Europe/Kiev</c>, <c>US/Eastern</c> and others) in a separate <c>tzdata-legacy</c> package. Browsers still
/// report some of them (Chromium sends <c>Asia/Calcutta</c> for India), so without it a viewer's zone can be
/// unknown to the server and their table views fall back to the tenant's zone. The check reports that as
/// <see cref="HealthStatus.Degraded"/>, visible without keeping the host out of rotation.
/// </para>
/// <para>Use <see cref="TimeZoneDataHealthCheckExtensions.AddBlueprintTimeZoneDataCheck"/>.</para>
/// </remarks>
public sealed class TimeZoneDataHealthCheck : IHealthCheck
{
    /// <summary>
    /// The zone resolved when no other is given: one with daylight saving time, so a host that knows only UTC
    /// fails the check.
    /// </summary>
    public static readonly string DefaultProbeZoneId = "Europe/Zurich";

    /// <summary>
    /// The legacy name resolved when no other is given: the one Chromium reports for India.
    /// </summary>
    public static readonly string DefaultLegacyProbeZoneId = "Asia/Calcutta";

    private readonly string probeZoneId;
    private readonly string legacyProbeZoneId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneDataHealthCheck"/> class that resolves
    /// <see cref="DefaultProbeZoneId"/> and <see cref="DefaultLegacyProbeZoneId"/>.
    /// </summary>
    public TimeZoneDataHealthCheck()
        : this(DefaultProbeZoneId)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneDataHealthCheck"/> class that resolves the given zone
    /// and <see cref="DefaultLegacyProbeZoneId"/>.
    /// </summary>
    /// <param name="probeZoneId">The IANA id of the zone to resolve, such as the tenants' default zone.</param>
    public TimeZoneDataHealthCheck(string probeZoneId)
        : this(probeZoneId, DefaultLegacyProbeZoneId)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneDataHealthCheck"/> class that resolves the given zones.
    /// </summary>
    /// <param name="probeZoneId">The IANA id of the zone to resolve, such as the tenants' default zone.</param>
    /// <param name="legacyProbeZoneId">The legacy IANA name whose absence degrades the check.</param>
    public TimeZoneDataHealthCheck(string probeZoneId, string legacyProbeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(probeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyProbeZoneId);

        this.probeZoneId = probeZoneId;
        this.legacyProbeZoneId = legacyProbeZoneId;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!ZonedTime.TryFindIanaTimeZone(this.probeZoneId, out _))
        {
            return Task.FromResult(new HealthCheckResult(
                context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                $"The time zone data cannot resolve the IANA zone '{this.probeZoneId}'. Install the tzdata package in the image."));
        }

        if (!ZonedTime.TryFindIanaTimeZone(this.legacyProbeZoneId, out _))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"The time zone data resolves '{this.probeZoneId}' but not the legacy name '{this.legacyProbeZoneId}', which " +
                "browsers still send. Requests naming such a zone fall back to the tenant's zone. Install the legacy " +
                "links (tzdata-legacy on Debian and Ubuntu)."));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"The time zone data resolves '{this.probeZoneId}' and the legacy name '{this.legacyProbeZoneId}'."));
    }
}
