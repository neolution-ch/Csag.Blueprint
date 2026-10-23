namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// What <see cref="IActiveSessionTracker"/> records about a session: the values of its authentication ticket, plus
/// the request details captured when the session is attributed to an account.
/// </summary>
/// <param name="SessionKey">The ticket store key the session cookie carries.</param>
/// <param name="UserId">The account the ticket authenticates.</param>
/// <param name="TenantId">The tenant the ticket is scoped to, if any.</param>
/// <param name="ExpiresAt">When the ticket expires.</param>
/// <param name="UserAgent">The user agent of the request that signed in, if known.</param>
/// <param name="IpAddress">The client address of the request that signed in, if known.</param>
public sealed record SessionTrackingEntry(
    string SessionKey,
    Guid UserId,
    Guid? TenantId,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? IpAddress);
