namespace Csag.Blueprint.Domain.Entities;

/// <summary>
/// Tracks active authentication sessions for users.
/// Used for immediate session revocation by mapping user IDs to their session keys in the cache.
/// This is a reusable platform entity owned by blueprint session infrastructure.
/// </summary>
public sealed class BlueprintActiveSession
{
    /// <summary>
    /// Gets or sets the unique identifier for this session record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID associated with this session.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the cache key (ticket store key) for this session.
    /// </summary>
    public required string SessionKey { get; set; }

    /// <summary>
    /// Gets or sets when this session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this session will expire.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the user agent string from the browser.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the current tenant identifier for this session.
    /// Tracks which tenant context the session is currently operating under.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }
}
