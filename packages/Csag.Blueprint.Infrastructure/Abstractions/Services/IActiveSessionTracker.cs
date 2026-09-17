namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Keeps the tracked session row in step with the authentication ticket it describes, for the two
/// operations the cookie handler drives through <c>ITicketStore</c>: sliding renewal and removal.
/// <para>
/// This is the singleton-safe subset of session tracking. It exists separately from
/// <c>ISessionManager</c> so the ticket store — a singleton — can write the row without resolving a
/// scoped service graph, and so untracking never has to run inside a cookie authentication event.
/// A <c>CookieSigningOutContext</c> does not carry the session key, so an event handler could only
/// recover it via <c>HttpContext.AuthenticateAsync</c> — and sign-out can be raised from inside the
/// cookie handler's own authentication pass, where re-entering returns that still-running task and
/// the request awaits itself forever.
/// </para>
/// </summary>
public interface IActiveSessionTracker
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

    /// <summary>
    /// Removes the tracked session row so it cannot outlive the ticket it describes.
    /// </summary>
    /// <param name="sessionKey">The session key whose tracking row should be removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a tracking row was found and removed; <c>false</c> when no row matches
    /// the key — either it was already revoked, or the key belongs to a cookie scheme that has no
    /// tracking rows at all.</returns>
    Task<bool> UntrackAsync(string sessionKey, CancellationToken cancellationToken = default);
}
