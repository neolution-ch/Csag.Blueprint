namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The <see cref="ITenantTimeZoneProvider"/> in effect when the application registers none: every tenant
/// is in UTC.
/// </summary>
public sealed class UtcTenantTimeZoneProvider : ITenantTimeZoneProvider
{
    /// <inheritdoc/>
    public ValueTask<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(TimeZoneInfo.Utc);
}
