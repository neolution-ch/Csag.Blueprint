namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.Extensions.Logging;

/// <summary>
/// The package's <see cref="IServiceAccountSessionLifecycle"/>: ends a service account's sessions and tells the
/// application's <see cref="ISessionRevocationParticipant"/>s.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class ServiceAccountSessionLifecycle : IServiceAccountSessionLifecycle
{
    private readonly IServiceAccountSessionManager sessions;
    private readonly IEnumerable<ISessionRevocationParticipant> participants;
    private readonly ILogger<ServiceAccountSessionLifecycle> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountSessionLifecycle"/> class.
    /// </summary>
    /// <param name="sessions">The service-account sessions.</param>
    /// <param name="participants">The application's revocation participants.</param>
    /// <param name="logger">The logger.</param>
    public ServiceAccountSessionLifecycle(
        IServiceAccountSessionManager sessions,
        IEnumerable<ISessionRevocationParticipant> participants,
        ILogger<ServiceAccountSessionLifecycle> logger)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task AccountDeactivatedAsync(Guid serviceAccountId)
        => this.RevokeAndNotifyAsync(serviceAccountId, SessionRevocationReason.AccessRevoked);

    /// <inheritdoc/>
    public Task AccountDeletedAsync(Guid serviceAccountId)
        => this.RevokeAndNotifyAsync(serviceAccountId, SessionRevocationReason.AccessRevoked);

    /// <summary>
    /// Ends every session of the account, then runs every participant, each step even when an earlier one failed.
    /// </summary>
    /// <param name="serviceAccountId">The account.</param>
    /// <param name="reason">The reason the participants are given.</param>
    /// <returns>A task that completes once every step has run.</returns>
    /// <exception cref="AggregateException">One or more steps failed; each failure is attached.</exception>
    public async Task RevokeAndNotifyAsync(Guid serviceAccountId, SessionRevocationReason reason)
    {
        var failures = new List<Exception>();

        try
        {
            await this.sessions.RevokeServiceAccountSessionsAsync(serviceAccountId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Revoking the sessions of a service account failed; the participants still run");
            failures.Add(ex);
        }

        // Participants hear about the revocation whether or not any session matched: the application's own
        // credentials for the account may exist without one.
        var revocation = new SessionRevocation { Reason = reason, Subject = SessionSubject.ServiceAccount(serviceAccountId) };
        foreach (var participant in this.participants)
        {
            try
            {
                await participant.OnSessionsRevokedAsync(revocation);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Session revocation participant {Participant} failed for a service-account {Reason} revocation",
                    participant.GetType().FullName,
                    reason);
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Service-account session revocation did not complete. Every step ran; each error is attached.", failures);
        }
    }
}
