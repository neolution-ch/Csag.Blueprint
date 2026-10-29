namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Describes one revocation to the <see cref="ISessionRevocationParticipant"/>s: why it happened, whose sessions
/// it covers, and how far it reaches.
/// </summary>
public sealed class SessionRevocation
{
    /// <summary>
    /// Gets the intent that revoked the sessions.
    /// </summary>
    public required SessionRevocationReason Reason { get; init; }

    /// <summary>
    /// Gets whose sessions were revoked, or <see langword="null"/> when the revocation is about a tenant rather than
    /// one account (<see cref="SessionRevocationReason.TenantRemoved"/>).
    /// </summary>
    public SessionSubject? Subject { get; init; }

    /// <summary>
    /// Gets the tenant the revocation is limited to, or <see langword="null"/> when it covers every tenant. Always
    /// set for <see cref="SessionRevocationReason.TenantRemoved"/>.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Gets the single session that was revoked, or <see langword="null"/> when the revocation covers every
    /// session in its scope. Set only for <see cref="SessionRevocationReason.SessionRevokedByOwner"/>.
    /// </summary>
    public Guid? SessionId { get; init; }
}
