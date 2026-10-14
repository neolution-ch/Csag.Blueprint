namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Ends credentials the application issues itself — refresh tokens, API keys, device bindings — whenever the
/// session lifecycle revokes sessions, so they cannot outlive the sessions they were issued alongside. Register any
/// number of implementations; every revoking intent of <see cref="ISessionLifecycle"/> calls all of them.
/// </summary>
/// <remarks>
/// <para>
/// Participants run one after another, in registration order, after the package has ended its own sessions. A
/// participant that throws does not stop the others: the intent collects every failure and reports them together
/// once all participants have run.
/// </para>
/// <para>
/// There is no <see cref="CancellationToken"/>, for the same reason the intents have none: revocation runs to
/// completion. An intent the caller retries calls every participant again, so an implementation must be safe to
/// run twice for the same revocation.
/// </para>
/// </remarks>
public interface ISessionRevocationParticipant
{
    /// <summary>
    /// Ends the application's own credentials covered by <paramref name="revocation"/>.
    /// </summary>
    /// <param name="revocation">What was revoked, for whom, and in which scope.</param>
    /// <returns>A task that completes once the participant's credentials are ended.</returns>
    Task OnSessionsRevokedAsync(SessionRevocation revocation);
}
