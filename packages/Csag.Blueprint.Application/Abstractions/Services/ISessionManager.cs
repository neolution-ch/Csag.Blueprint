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
    /// Revokes all active sessions for a specific user, across every tenant the (shared) account is
    /// signed in to. Use this for account-level lifecycle changes (credential resets, email changes,
    /// account disable) where every session must be invalidated.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the user's active sessions that are currently scoped to a specific tenant. Sessions the
    /// (shared) account holds in other tenants are left intact. Use this for tenant-scoped access changes
    /// (removing a member from a tenant, an admin revoking a member's sessions within their tenant).
    /// </summary>
    /// <param name="userId">The user ID whose tenant-scoped sessions should be revoked.</param>
    /// <param name="tenantId">The tenant to scope the revocation to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeUserSessionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all of the user's active sessions EXCEPT the one identified by <paramref name="keepSessionKey"/>.
    /// Use this for self-service security changes the user makes on their own account (changing their password,
    /// enabling or disabling MFA): every OTHER device is signed out immediately while the session that performed
    /// the change stays alive, so the user is not bounced to the login screen for acting on their own account.
    /// </summary>
    /// <param name="userId">The user ID whose other sessions should be revoked.</param>
    /// <param name="keepSessionKey">
    /// The session key to preserve — typically the current request's session
    /// (see <c>HttpContext.GetCurrentSessionKeyAsync()</c>). Must be non-empty; to revoke every session
    /// including the current one, call <see cref="RevokeUserSessionsAsync(Guid, CancellationToken)"/> instead.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked (never counts the preserved session).</returns>
    Task<int> RevokeOtherUserSessionsAsync(Guid userId, string keepSessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active session — for any user — that is currently scoped to the given tenant.
    /// Use this for tenant-level lifecycle changes (deleting a tenant) so no session keeps operating
    /// against a tenant that no longer exists. Sessions the affected accounts hold in other tenants
    /// are left intact.
    /// </summary>
    /// <param name="tenantId">The tenant whose sessions should be revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeTenantSessionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

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
    /// Refreshes the user's active sessions that are currently scoped to a specific tenant by updating
    /// their cached roles and permissions. Sessions the (shared) account holds in other tenants are left
    /// untouched. Use this for tenant-scoped authorization changes that cannot affect the user's
    /// authorization anywhere else.
    /// </summary>
    /// <param name="userId">The user ID whose sessions should be refreshed.</param>
    /// <param name="tenantId">The tenant to scope the refresh to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sessions refreshed.</returns>
    Task<int> RefreshUserSessionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

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
    /// Gets or sets the identifier of the session tracking row.
    /// </summary>
    public Guid Id { get; set; }

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

    /// <summary>
    /// Gets or sets the tenant this session is currently scoped to, if any.
    /// Used to rebuild tenant-scoped authorization claims when refreshing sessions.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }
}
