namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Supplies the time zone of the current tenant: the zone of the place whose clock gives the tenant's
/// local readings their meaning, and the zone that decides what "today" and a calendar day are for the
/// tenant. Implemented by the application, which knows where the tenant stores its zone; the packages
/// register a fallback that always answers UTC, so they work before the application provides one.
/// </summary>
/// <remarks>
/// Server-side work that should follow the viewer's zone instead (a date filter or an export the viewer
/// asked for) takes the zone from a declared request field and falls back to this provider only when the
/// request carries none.
/// </remarks>
public interface ITenantTimeZoneProvider
{
    /// <summary>
    /// Gets the time zone of the current tenant.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>
    /// The tenant's zone, or <see cref="TimeZoneInfo.Utc"/> when there is no current tenant or the tenant has
    /// no zone. Never null.
    /// </returns>
    ValueTask<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default);
}
