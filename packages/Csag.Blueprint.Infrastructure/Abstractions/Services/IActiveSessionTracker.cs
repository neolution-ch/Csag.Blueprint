namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Keeps the tracked session row in step with the authentication ticket it describes, for the operations the cookie
/// handler drives through <c>ITicketStore</c>: sign-in, sliding renewal and removal. The row follows the ticket —
/// whichever account and tenant the ticket last written under a key authenticates, the row says the same.
/// <para>
/// This is the singleton-safe part of session tracking, separate from the scoped session lifecycle, so the ticket
/// store — a singleton — can write the row without resolving a scoped service graph, and so tracking never has to
/// run inside a cookie authentication event. A <c>CookieSigningOutContext</c> does not carry the session key, so an
/// event handler could only recover it via <c>HttpContext.AuthenticateAsync</c> — and sign-out can be raised from
/// inside the cookie handler's own authentication pass, where re-entering returns that still-running task and the
/// request awaits itself forever.
/// </para>
/// <para>
/// Register a decorator by resolving the package's <c>ActiveSessionTracker&lt;TContext&gt;</c> as the inner
/// instance.
/// </para>
/// </summary>
public interface IActiveSessionTracker
{
    /// <summary>
    /// Records a signed-in session. Creates the row, or — when a row already exists under the key, because the
    /// sign-in reused the key of a live session cookie — attributes that row to the signing-in account (see
    /// <see cref="RenewAsync"/>).
    /// </summary>
    /// <param name="entry">The session to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">The session key is blank, or longer than a tracked session can carry.
    /// Thrown at the call site rather than on the returned task.</exception>
    Task TrackAsync(SessionTrackingEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings an existing row in line with a renewed ticket. The row takes the ticket's expiry and tenant. When the
    /// ticket authenticates a different account than the row — a sign-in over a live session cookie reuses that
    /// cookie's key — the row is attributed to the new account, with its creation time, user agent and address
    /// reset to the new sign-in's.
    /// </summary>
    /// <param name="entry">The renewed session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a row exists under the key and was updated; <c>false</c> when no row matches —
    /// the session was revoked (or its expired row cleaned up), so the caller must treat the session as no longer
    /// valid rather than keep renewing its ticket.</returns>
    Task<bool> RenewAsync(SessionTrackingEntry entry, CancellationToken cancellationToken = default);

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
