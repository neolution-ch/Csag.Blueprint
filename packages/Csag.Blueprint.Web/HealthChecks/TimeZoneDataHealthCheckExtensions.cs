namespace Csag.Blueprint.Web.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Registers <see cref="TimeZoneDataHealthCheck"/>.
/// </summary>
public static class TimeZoneDataHealthCheckExtensions
{
    /// <summary>
    /// The name the check is registered under by default.
    /// </summary>
    public static readonly string DefaultName = "time_zone_data_check";

    /// <summary>
    /// The tag the check carries by default, which selects it for the readiness probe.
    /// </summary>
    public static readonly string DefaultTag = "ready";

    /// <summary>
    /// Adds a check that reports <see cref="HealthStatus.Unhealthy"/> when the host has no time zone data,
    /// tagged <c>ready</c> so the readiness probe keeps such a host out of rotation.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The registration name; <c>time_zone_data_check</c> when null.</param>
    /// <param name="tags">The tags; <c>ready</c> when null.</param>
    /// <param name="probeZoneId">The IANA id of the zone to resolve; <see cref="TimeZoneDataHealthCheck.DefaultProbeZoneId"/> when null.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddBlueprintTimeZoneDataCheck(
        this IHealthChecksBuilder builder,
        string? name = null,
        IEnumerable<string>? tags = null,
        string? probeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCheck(
            name ?? DefaultName,
            new TimeZoneDataHealthCheck(probeZoneId ?? TimeZoneDataHealthCheck.DefaultProbeZoneId),
            HealthStatus.Unhealthy,
            tags ?? [DefaultTag]);
    }
}
