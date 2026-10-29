namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The result of <see cref="ISessionLifecycle.SwitchTenantAsync"/>.
/// </summary>
public enum SessionTenantSwitchOutcome
{
    /// <summary>
    /// The request carries no session to switch: it did not authenticate with the session cookie, or the session's
    /// tracking record no longer exists. The session is unchanged.
    /// </summary>
    NoActiveSession = 0,

    /// <summary>
    /// The session's cached ticket, or the account it belongs to, is gone. The session is unchanged.
    /// </summary>
    SessionExpired = 1,

    /// <summary>
    /// The caller's account is not a member of the target tenant. The session is unchanged.
    /// </summary>
    NotAMember = 2,

    /// <summary>
    /// The caller's session now runs in the target tenant, with claims rebuilt for it.
    /// </summary>
    Switched = 3,
}
