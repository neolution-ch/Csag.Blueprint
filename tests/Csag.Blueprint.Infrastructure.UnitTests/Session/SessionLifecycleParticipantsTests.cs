namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for how <see cref="ISessionLifecycle"/> involves the application's revocation participants: every
/// revoking intent tells all of them, each with the subject and scope it revoked, a failing participant does not stop
/// the rest, and every failure is reported together once all have run. The sessions themselves are ended against SQL
/// Server in the integration suite; here no session matches, which is enough to drive the participants.
/// </summary>
public sealed class SessionLifecycleParticipantsTests
{
    private static readonly Guid AccountId = new Guid("0d6f1a2b-3c4d-4e5f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid TenantId = new Guid("7e8f9a0b-1c2d-4e3f-9a4b-5c6d7e8f9a0b");

    [Fact]
    public async Task AccessRevoked_RunsEveryParticipantAndReportsTheFailuresTogether()
    {
        // Arrange — the first and last participants fail; the one between them must still run.
        var first = new FailingParticipant("first");
        var recording = new RecordingParticipant();
        var last = new FailingParticipant("last");
        using var services = BuildServices(first, recording, last);
        using var scope = services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<ISessionLifecycle>();

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => lifecycle.AccessRevokedAsync(AccountId));

        // Assert
        exception.InnerExceptions.Select(e => e.Message).ShouldBe(["first", "last"]);
        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.AccessRevoked);
        revocation.Subject.ShouldBe(SessionSubject.User(AccountId));
        revocation.TenantId.ShouldBeNull();
        revocation.SessionId.ShouldBeNull();
    }

    [Fact]
    public async Task AccessRevokedInATenant_CarriesTheTenant()
    {
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISessionLifecycle>().AccessRevokedAsync(AccountId, TenantId);

        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Subject.ShouldBe(SessionSubject.User(AccountId));
        revocation.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task CredentialsChanged_OutsideARequest_TellsTheParticipants()
    {
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISessionLifecycle>().CredentialsChangedAsync(AccountId);

        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.CredentialsChanged);
        revocation.Subject.ShouldBe(new SessionSubject(SessionSubjectKind.User, AccountId));
    }

    [Fact]
    public async Task TenantRemoved_IsAboutTheTenantRatherThanAnAccount()
    {
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISessionLifecycle>().TenantRemovedAsync(TenantId);

        var revocation = recording.Revocations.ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.TenantRemoved);
        revocation.Subject.ShouldBeNull();
        revocation.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task AccessRevoked_WhenThePackagesOwnStepFails_StillRunsTheParticipants()
    {
        // Arrange — a database that cannot be reached fails the package's own revocation.
        var recording = new RecordingParticipant();
        using var services = BuildServices(
            [recording],
            s => s.AddSingleton<IDbContextFactory<TestDbContext>>(new UnreachableDbContextFactory()));
        using var scope = services.CreateScope();

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(
            () => scope.ServiceProvider.GetRequiredService<ISessionLifecycle>().AccessRevokedAsync(AccountId));

        // Assert
        exception.InnerExceptions.ShouldHaveSingleItem().Message.ShouldBe("database unreachable");
        recording.Revocations.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task IntentsThatEndNoSessionOrNeedTheCaller_DoNotInvolveTheParticipants()
    {
        // Arrange — outside a request there is no caller session, so the caller-scoped intents have nothing to act on.
        var recording = new RecordingParticipant();
        using var services = BuildServices(recording);
        using var scope = services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<ISessionLifecycle>();

        // Act
        await lifecycle.ClaimsChangedAsync(AccountId);
        await lifecycle.ClaimsChangedAsync(AccountId, TenantId);
        var outcome = await lifecycle.SwitchTenantAsync(TenantId);
        var sessions = await lifecycle.ListMySessionsAsync();
        await lifecycle.RevokeMySessionAsync(Guid.NewGuid());

        // Assert
        outcome.ShouldBe(SessionTenantSwitchOutcome.NoActiveSession);
        sessions.ShouldBeEmpty();
        recording.Revocations.ShouldBeEmpty();
    }

    private static ServiceProvider BuildServices(params ISessionRevocationParticipant[] participants)
        => BuildServices(participants, before: null);

    private static ServiceProvider BuildServices(ISessionRevocationParticipant[] participants, Action<IServiceCollection>? before)
    {
        return SessionTestServices.Build(before: services =>
        {
            before?.Invoke(services);
            foreach (var participant in participants)
            {
                services.AddSingleton(participant);
            }
        });
    }

    private sealed class UnreachableDbContextFactory : IDbContextFactory<TestDbContext>
    {
        public TestDbContext CreateDbContext() => throw new InvalidOperationException("database unreachable");
    }

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
