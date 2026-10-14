namespace Csag.Blueprint.Infrastructure.Session;

/// <summary>
/// Settings for the cookie session infrastructure registered by
/// <see cref="ServiceCollectionExtensions.AddBlueprintSessionInfrastructure{TUser, TContext}"/> and
/// <see cref="ServiceCollectionExtensions.AddBlueprintCookieSessions"/>.
/// </summary>
public sealed class BlueprintSessionOptions
{
    /// <summary>
    /// Gets or sets the session lifetime: the cookie's <c>ExpireTimeSpan</c>, and the ticket's <c>ExpiresUtc</c>
    /// when a sign-in does not set one. Sliding expiration is always on, so this is an idle timeout: a session that
    /// is used renews once more than half of it has passed.
    /// </summary>
    /// <remarks>
    /// <c>Csag.Blueprint.Web</c> sets this from <c>Blueprint:Security:SessionExpirationHours</c> after every other
    /// configuration step, so that setting stays the single source of the lifetime. Set it yourself only in a host
    /// that does not register the Web package's options. Must be positive; startup fails otherwise.
    /// </remarks>
    public TimeSpan Lifetime { get; set; }
}
