namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// One session in the list <see cref="ISessionLifecycle.ListMySessionsAsync"/> returns. The session key the cookie
/// carries is deliberately absent: it is a bearer credential, and <see cref="Id"/> is what identifies a session to
/// <see cref="ISessionLifecycle.RevokeMySessionAsync"/>.
/// </summary>
public sealed class ActiveSessionInfo
{
    /// <summary>
    /// Gets or sets the identifier of the session tracking row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the user agent of the request that signed in.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the client IP address of the request that signed in.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the tenant this session is currently scoped to, if any.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the session the current request authenticated with.
    /// </summary>
    public bool IsCurrent { get; set; }
}
