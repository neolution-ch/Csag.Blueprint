namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The intent behind a <see cref="SessionRevocation"/>.
/// </summary>
public enum SessionRevocationReason
{
    /// <summary>
    /// The account's credentials changed (<see cref="ISessionLifecycle.CredentialsChangedAsync"/>). The caller's own
    /// session may have been kept.
    /// </summary>
    CredentialsChanged = 0,

    /// <summary>
    /// The account lost access, everywhere or in <see cref="SessionRevocation.TenantId"/>
    /// (<c>ISessionLifecycle.AccessRevokedAsync</c>).
    /// </summary>
    AccessRevoked = 1,

    /// <summary>
    /// The tenant in <see cref="SessionRevocation.TenantId"/> was removed
    /// (<see cref="ISessionLifecycle.TenantRemovedAsync"/>).
    /// </summary>
    TenantRemoved = 2,

    /// <summary>
    /// The account's owner ended one of its sessions, <see cref="SessionRevocation.SessionId"/>
    /// (<see cref="ISessionLifecycle.RevokeMySessionAsync"/>).
    /// </summary>
    SessionRevokedByOwner = 3,
}
