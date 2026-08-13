namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Keeps the tracked session row's <c>ExpiresAt</c> in step with the authentication ticket when
/// sliding expiration renews it. Without this, a long-lived active session outlives its tracking
/// row and becomes invisible to session listing, revocation, and refresh — so authorization
/// changes (e.g. a demotion) would never reach it.
/// </summary>
public interface ISessionExpirationExtender
{
    /// <summary>
    /// Updates the tracked session's expiration to the renewed ticket expiration.
    /// </summary>
    /// <param name="sessionKey">The session key that identifies the tracked session.</param>
    /// <param name="expiresUtc">The renewed absolute expiration time in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the tracked session row exists and was extended; <c>false</c> when no
    /// row matches the key — the session was revoked (or its lagging row expired and was cleaned up),
    /// so the caller must treat the session as no longer valid rather than keep renewing its ticket.</returns>
    Task<bool> ExtendAsync(string sessionKey, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default);
}
