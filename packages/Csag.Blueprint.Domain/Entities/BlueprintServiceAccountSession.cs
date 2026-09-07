namespace Csag.Blueprint.Domain.Entities;

/// <summary>
/// Tracks active service-account authentication sessions (the server-side backing record for a
/// reference-style JWT). A service-account token carries only an opaque session key; this row makes
/// the session revocable and lets deactivation / secret rotation take effect immediately by dropping
/// the record (and its cached marker) instead of waiting for the token to expire.
/// This is a reusable platform entity owned by blueprint session infrastructure and is intentionally
/// separate from <see cref="BlueprintActiveSession"/>, which models user (cookie) sessions.
/// </summary>
public sealed class BlueprintServiceAccountSession
{
    /// <summary>
    /// Gets or sets the unique identifier for this session record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the service account this session was issued for.
    /// </summary>
    public Guid ServiceAccountId { get; set; }

    /// <summary>
    /// Gets or sets the opaque session key (the value carried by the token's session-id claim and the
    /// cache key for the reference-token backing store).
    /// </summary>
    public required string SessionKey { get; set; }

    /// <summary>
    /// Gets or sets when this session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this session expires. Mirrors the issued token's expiry so an expired row can be
    /// treated as revoked even if the distributed-cache marker was lost.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the user agent string of the client that requested the token, if any.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client that requested the token, if any.
    /// </summary>
    public string? IpAddress { get; set; }
}
