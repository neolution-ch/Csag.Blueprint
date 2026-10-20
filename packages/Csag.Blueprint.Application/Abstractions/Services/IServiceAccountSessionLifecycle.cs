namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The service-account counterpart of <see cref="ISessionLifecycle"/>: the application states what happened to a
/// service account, and the package ends the account's sessions — and with them its bearer tokens — and tells every
/// <see cref="ISessionRevocationParticipant"/>, with a <see cref="SessionSubject"/> of kind
/// <see cref="SessionSubjectKind.ServiceAccount"/>.
/// </summary>
/// <remarks>
/// <para>
/// Call an intent after the change it reports has been committed. No intent takes a
/// <see cref="CancellationToken"/>: once called it runs to completion.
/// </para>
/// <para>
/// Each intent runs every step even when one fails, then throws an <see cref="AggregateException"/> carrying each
/// failure. Every intent is safe to call again with the same argument.
/// </para>
/// <para>
/// Role, permission and name changes need no intent: every request resolves them from the account's current state.
/// </para>
/// </remarks>
public interface IServiceAccountSessionLifecycle
{
    /// <summary>
    /// The account was deactivated. Ends all of its sessions; its tokens are refused from the next request on.
    /// </summary>
    /// <param name="serviceAccountId">The deactivated account.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task AccountDeactivatedAsync(Guid serviceAccountId);

    /// <summary>
    /// The account was deleted. Ends all of its sessions, which would otherwise stay stored until they expire.
    /// </summary>
    /// <param name="serviceAccountId">The deleted account.</param>
    /// <returns>A task that completes once the sessions are ended and every participant has run.</returns>
    Task AccountDeletedAsync(Guid serviceAccountId);
}
