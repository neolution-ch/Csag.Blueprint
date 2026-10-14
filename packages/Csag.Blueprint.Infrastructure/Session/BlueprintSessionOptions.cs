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

    /// <summary>
    /// Gets or sets the name of the request header the edge stamps with the client's address, for example
    /// <c>X-Client-IP</c>. Session tracking records the address from this header, read with the same rule the
    /// package's rate limiting uses: the last entry, the last comma-separated token in it, accepted only when it
    /// parses as an IP address. Without the header, or when it is not set, the connection's remote address is
    /// recorded.
    /// </summary>
    /// <remarks>
    /// Behind a load balancer that appends its own <c>X-Forwarded-For</c> entry, the connection's remote address
    /// is the balancer or a proxy, not the client. Name a header here only when the edge overwrites it on every
    /// request, so a client cannot supply its own value.
    /// </remarks>
    public string? ClientIpHeader { get; set; }
}
