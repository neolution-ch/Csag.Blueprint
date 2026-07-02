namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Manages authentication session lifecycle operations.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Tracks a new active session for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="sessionKey">The session key (ticket store key).</param>
    /// <param name="expiresAt">When the session expires.</param>
    /// <param name="userAgent">The user agent string from the browser.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    /// <param name="currentTenantId">The current tenant ID for this session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TrackSessionAsync(
        Guid userId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        Guid? currentTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific session by its session key.
    /// </summary>
    /// <param name="sessionKey">The session key to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session was found and revoked, false otherwise.</returns>
    Task<bool> RevokeSessionAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a session from the database without affecting the cache.
    /// Used by cookie authentication events during normal logout flow.
    /// For manual revocation (admin actions), use RevokeSessionAsync instead.
    /// </summary>
    /// <param name="sessionKey">The session key to untrack.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the session was found and removed, false otherwise.</returns>
    Task<bool> UntrackSessionAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of active session information.</returns>
    Task<List<ActiveSessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes all active sessions for a user by updating their cached roles and permissions.
    /// Called when a user's roles or permissions are modified to ensure sessions immediately reflect the changes.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be refreshed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions refreshed.</returns>
    Task<int> RefreshUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired session records from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions cleaned up.</returns>
    Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about an active session.
/// </summary>
public sealed class ActiveSessionInfo
{
    /// <summary>
    /// Gets or sets the session key.
    /// </summary>
    public required string SessionKey { get; set; }

    /// <summary>
    /// Gets or sets when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the user agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the IP address.
    /// </summary>
    public string? IpAddress { get; set; }
}
