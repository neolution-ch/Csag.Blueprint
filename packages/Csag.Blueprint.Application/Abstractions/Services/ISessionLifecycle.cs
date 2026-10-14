namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The application's entry point into the cookie session lifecycle. Each method states an intent — what happened
/// to an account, to a tenant, or to the caller's own sessions — and the implementation decides which sessions end,
/// which keep running with refreshed claims, and which <see cref="ISessionRevocationParticipant"/>s hear about it.
/// </summary>
/// <remarks>
/// <para>
/// No method takes a <see cref="CancellationToken"/>: once called, an intent runs to completion. Call it after the
/// change it reports has been committed, so a client that disconnects cannot leave sessions running on access the
/// database no longer grants.
/// </para>
/// <para>
/// Failures throw. The revoking intents (<see cref="CredentialsChangedAsync"/>, both <c>AccessRevokedAsync</c>
/// overloads, <see cref="TenantRemovedAsync"/> and <see cref="RevokeMySessionAsync"/>) run every step even when one
/// fails, then throw an <see cref="AggregateException"/> carrying each failure. Every intent is safe to call again
/// with the same arguments, so a caller that sees a failure retries the intent rather than compensating for it.
/// </para>
/// <para>
/// "The caller's session" is the Identity application cookie session the current HTTP request authenticated with.
/// Outside a request, or on a request without that cookie, there is none.
/// </para>
/// </remarks>
public interface ISessionLifecycle
{
    /// <summary>
    /// The account's credentials changed: a password change or reset, or a second factor enabled or disabled. Ends
    /// every session of the account except the caller's own, when the caller's session belongs to that account.
    /// With no caller session (a reset from an emailed link, a setup step that runs before a full session exists, a
    /// background job), or one belonging to another account, every session of the account ends.
    /// </summary>
    /// <param name="accountId">The account whose credentials changed.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task CredentialsChangedAsync(Guid accountId);

    /// <summary>
    /// The account lost access everywhere: it was disabled, deleted, or its sign-in identity changed. Ends every
    /// session of the account, the caller's included.
    /// </summary>
    /// <param name="accountId">The account whose access was revoked.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task AccessRevokedAsync(Guid accountId);

    /// <summary>
    /// The account lost access to one tenant: its membership was removed, or an administrator ended its sessions
    /// there. Ends the account's sessions scoped to <paramref name="tenantId"/>; its sessions in other tenants keep
    /// running.
    /// </summary>
    /// <param name="accountId">The account whose access was revoked.</param>
    /// <param name="tenantId">The tenant the account lost access to.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task AccessRevokedAsync(Guid accountId, Guid tenantId);

    /// <summary>
    /// Something the account's session claims are built from changed: its profile, preferred language, platform
    /// roles, or tenant roles and permissions. Rebuilds the cached claims of every session of the account, in each
    /// session's own tenant, so the change applies without signing in again.
    /// </summary>
    /// <param name="accountId">The account whose claims changed.</param>
    /// <returns>A task that completes once every session's claims are rebuilt.</returns>
    Task ClaimsChangedAsync(Guid accountId);

    /// <summary>
    /// The account's roles or permissions changed in one tenant only. Rebuilds the cached claims of the account's
    /// sessions scoped to <paramref name="tenantId"/>; its sessions in other tenants are left as they are.
    /// </summary>
    /// <param name="accountId">The account whose claims changed.</param>
    /// <param name="tenantId">The tenant the change applies to.</param>
    /// <returns>A task that completes once the affected sessions' claims are rebuilt.</returns>
    Task ClaimsChangedAsync(Guid accountId, Guid tenantId);

    /// <summary>
    /// The tenant was removed. Ends every session, of any account, scoped to it. The accounts' sessions in other
    /// tenants keep running.
    /// </summary>
    /// <param name="tenantId">The removed tenant.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task TenantRemovedAsync(Guid tenantId);

    /// <summary>
    /// Moves the caller's session to another tenant: checks that the caller is a member of it, then rebuilds the
    /// session's tenant, profile and authorization claims for that tenant. Only the caller's session moves; the
    /// account's other sessions stay where they are.
    /// </summary>
    /// <param name="tenantId">The tenant to switch the caller's session to.</param>
    /// <returns>What happened; the session is unchanged unless the outcome is
    /// <see cref="SessionTenantSwitchOutcome.Switched"/>.</returns>
    Task<SessionTenantSwitchOutcome> SwitchTenantAsync(Guid tenantId);

    /// <summary>
    /// Lists the sessions of the caller's account that have not expired, newest first, flagging the caller's own.
    /// Without a caller session the list is empty.
    /// </summary>
    /// <returns>The caller's account's sessions.</returns>
    Task<IReadOnlyList<ActiveSessionInfo>> ListMySessionsAsync();

    /// <summary>
    /// Ends one session of the caller's account, identified by <see cref="ActiveSessionInfo.Id"/>. A session that
    /// does not belong to the caller's account, or no longer exists, is left alone. Ending the caller's own session
    /// signs the caller out, clearing the session cookie on the current response.
    /// </summary>
    /// <param name="sessionId">The session to end.</param>
    /// <returns>A task that completes once the session is ended and every participant has run.</returns>
    Task RevokeMySessionAsync(Guid sessionId);
}
