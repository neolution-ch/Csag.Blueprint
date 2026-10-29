namespace Csag.Blueprint.Infrastructure.UnitTests.ServiceAccounts;

using Csag.Blueprint.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for how <see cref="IServiceAccountSessionLifecycle"/> involves the application's revocation participants:
/// both intents tell every participant, with the service account as the subject, a failing participant does not stop
/// the rest, and every failure is reported together. The sessions themselves are ended against SQL Server in the
/// integration suite; here no session matches, which is enough to drive the participants.
/// </summary>
public sealed class ServiceAccountSessionLifecycleTests
{
    private static readonly Guid ServiceAccountId = new("5c1f0a2b-3d4e-4f50-8a6b-7c8d9e0f1a2b");

    [Fact]
    public async Task AccountDeactivated_TellsEveryParticipantWithTheServiceAccountAsTheSubject()
    {
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IServiceAccountSessionLifecycle>().AccountDeactivatedAsync(ServiceAccountId);

        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.AccessRevoked);
        revocation.Subject.ShouldBe(SessionSubject.ServiceAccount(ServiceAccountId));
        revocation.Subject!.Value.Kind.ShouldBe(SessionSubjectKind.ServiceAccount);
        revocation.TenantId.ShouldBeNull();
        revocation.SessionId.ShouldBeNull();
    }

    [Fact]
    public async Task AccountDeleted_TellsEveryParticipantWithTheServiceAccountAsTheSubject()
    {
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IServiceAccountSessionLifecycle>().AccountDeletedAsync(ServiceAccountId);

        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.AccessRevoked);
        revocation.Subject.ShouldBe(SessionSubject.ServiceAccount(ServiceAccountId));
    }

    [Fact]
    public async Task AFailingParticipant_DoesNotStopTheOthersAndEveryFailureIsReported()
    {
        // Arrange — the first and last participants fail; the one between them must still run.
        var recording = new RecordingParticipant();
        using var services = BuildServices(new FailingParticipant("first"), recording, new FailingParticipant("last"));
        using var scope = services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IServiceAccountSessionLifecycle>();

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => lifecycle.AccountDeactivatedAsync(ServiceAccountId));

        // Assert
        exception.InnerExceptions.Select(e => e.Message).ShouldBe(["first", "last"]);
        recording.Revocations.ShouldHaveSingleItem().Subject.ShouldBe(SessionSubject.ServiceAccount(ServiceAccountId));
    }

    private static ServiceProvider BuildServices(params ISessionRevocationParticipant[] participants)
        => ServiceAccountTestServices.Build(after: services =>
        {
            foreach (var participant in participants)
            {
                services.AddSingleton(participant);
            }
        });

    private sealed class RecordingParticipant : ISessionRevocationParticipant
    {
        public List<SessionRevocation> Revocations { get; } = [];

        public Task OnSessionsRevokedAsync(SessionRevocation revocation)
        {
            this.Revocations.Add(revocation);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingParticipant(string message) : ISessionRevocationParticipant
    {
        public Task OnSessionsRevokedAsync(SessionRevocation revocation) => throw new InvalidOperationException(message);
    }
}
