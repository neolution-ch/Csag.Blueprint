namespace Csag.Blueprint.Infrastructure.Session;

/// <summary>
/// Manages the lifecycle of server-side service-account sessions that back reference-style JWTs.
/// Issuance tracks a session; every request resolves it (and the account's CURRENT authorization)
/// server-side; deactivation, secret rotation, and explicit revocation drop the session so outstanding
/// tokens stop working immediately instead of remaining valid until they expire.
/// </summary>
/// <remarks>
/// Applications reach these sessions only through the service-account intents
/// (<c>IServiceAccountCredentials</c> and <c>IServiceAccountSessionLifecycle</c>), which decide when a session is
/// tracked or revoked; the expired rows are deleted by <see cref="ExpiredSessionCleanupService{TContext}"/>.
/// </remarks>
internal interface IServiceAccountSessionManager
{
    /// <summary>
    /// Tracks a new service-account session. Persists a tracking row and primes the fast-path
    /// distributed-cache marker so subsequent requests can validate the session without a database read.
    /// </summary>
    /// <param name="serviceAccountId">The service account the session was issued for.</param>
    /// <param name="sessionKey">
    /// The opaque session key (also the token's session-id claim value). It is stored and looked up verbatim,
    /// never truncated to fit, and must be unique across all service-account sessions. The built-in
    /// implementation throws <see cref="ArgumentException"/> for a blank key, or one whose URL-encoded form
    /// exceeds 220 bytes — 220 characters for a base64url or hex key, fewer once characters outside the URI
    /// unreserved set are escaped.
    /// </param>
    /// <param name="expiresAt">When the session expires (mirrors the issued token's expiry).</param>
    /// <param name="userAgent">The user agent string of the requesting client, if available.</param>
    /// <param name="ipAddress">The IP address of the requesting client, if available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TrackSessionAsync(
        Guid serviceAccountId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a session key to the service account's CURRENT state, or <see langword="null"/> when the session is
    /// missing/revoked/expired or the account is missing or inactive. The returned identity, roles, permissions, and
    /// tenant are read from the database (never trusted from the token) so renames, role/permission changes and
    /// deactivation take effect per request.
    /// </summary>
    /// <param name="sessionKey">
    /// The opaque session key from the token's session-id claim, in the form the claim is read: a token carrying
    /// no such claim yields <see langword="null"/> from <c>FindFirstValue</c>. A missing, blank or over-long key
    /// is reported as "no session" rather than as an error, since no such session can have been tracked.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved session, or <see langword="null"/> if the request must be rejected.</returns>
    Task<ServiceAccountSessionValidation?> ValidateSessionAsync(string? sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single service-account session by its key, removing both the tracking row and the cached
    /// marker so any outstanding token referencing it is rejected on the next request.
    /// </summary>
    /// <param name="sessionKey">
    /// The session key to revoke. A blank or over-long key reports <see langword="false"/> rather than raising,
    /// since no such session can have been tracked.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if a session was found and revoked; otherwise <see langword="false"/>.</returns>
    Task<bool> RevokeSessionAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every outstanding session for a service account. Used for account-level lifecycle changes
    /// (secret rotation, deactivation, deletion) so all previously issued tokens stop working immediately.
    /// </summary>
    /// <param name="serviceAccountId">The service account whose sessions should be revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeServiceAccountSessionsAsync(Guid serviceAccountId, CancellationToken cancellationToken = default);
}
