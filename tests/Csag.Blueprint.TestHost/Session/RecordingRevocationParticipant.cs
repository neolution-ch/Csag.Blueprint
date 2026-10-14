namespace Csag.Blueprint.TestHost.Session;

using System.Collections.Concurrent;
using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// A revocation participant that records every revocation it is told about, standing in for an application's own
/// credentials (refresh tokens, API keys) so tests can see which revocations reach the application.
/// </summary>
public sealed class RecordingRevocationParticipant : ISessionRevocationParticipant
{
    private readonly ConcurrentQueue<SessionRevocation> revocations = new();

    /// <summary>
    /// Gets every revocation recorded since the host started, oldest first. The host is shared by the whole test run,
    /// so a test filters by the subject, tenant or session it created.
    /// </summary>
    public IReadOnlyCollection<SessionRevocation> Revocations => this.revocations.ToArray();

    /// <inheritdoc/>
    public Task OnSessionsRevokedAsync(SessionRevocation revocation)
    {
        this.revocations.Enqueue(revocation);
        return Task.CompletedTask;
    }
}
