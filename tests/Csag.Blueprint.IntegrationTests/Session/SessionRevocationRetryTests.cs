namespace Csag.Blueprint.IntegrationTests.Session;

using System.Net;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// Integration tests for revoking intents whose ticket removal fails, against the real SQL container and the host's
/// ticket cache: the session that could not be ended keeps its tracking row, so retrying the intent ends it, and
/// <see cref="ISessionLifecycle.RevokeMySessionAsync"/> still tells the participants on the failing attempt. Each test
/// composes its own session lifecycle over the host's database and cache, with a cache that refuses one removal.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SessionRevocationRetryTests(AppFixture app) : SessionTestBase(app)
{
    [Fact]
    public async Task AccessRevoked_WhenATicketCannotBeRemoved_KeepsItsRowSoARetryEndsItAsync()
    {
        // Arrange — one account on two devices; the cache refuses to remove the first device's ticket.
        var user = await this.CreateTestUserAsync("revoke_retry_ticket_test@test.local", SeedData.TenantAId);
        using var stuckDevice = await this.SignInAsync(user.Email!);
        var stuckKey = (await this.GetSingleSessionAsync(user.Id)).SessionKey;
        using var otherDevice = await this.SignInAsync(user.Email!);
        var cache = new RemovalRefusingTicketCache(this.App.Services.GetRequiredService<ITicketCacheService>(), stuckKey);
        await using var services = this.BuildLifecycleServices(cache, new RecordingParticipant());

        // Act
        await Should.ThrowAsync<AggregateException>(() => RunIntentAsync(services, lifecycle => lifecycle.AccessRevokedAsync(user.Id)));

        // Assert — the other device ended; the stuck one kept its row...
        await ShouldAuthenticateAsync(otherDevice, HttpStatusCode.Unauthorized);
        (await this.RemainingKeysAsync(user.Id)).ShouldBe([stuckKey]);

        // ...so retrying the intent finds it and ends it.
        cache.Refuse = false;
        await RunIntentAsync(services, lifecycle => lifecycle.AccessRevokedAsync(user.Id));
        await ShouldAuthenticateAsync(stuckDevice, HttpStatusCode.Unauthorized);
        (await this.RemainingKeysAsync(user.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RevokeMySession_WhenTheTicketCannotBeRemoved_TellsTheParticipantsAndARetryEndsItAsync()
    {
        // Arrange — the caller ends another of its account's sessions, whose ticket the cache refuses to remove.
        var user = await this.CreateTestUserAsync("revoke_my_retry_ticket_test@test.local", SeedData.TenantAId);
        using var otherDevice = await this.SignInAsync(user.Email!);
        var otherRow = await this.GetSingleSessionAsync(user.Id);
        using var callerDevice = await this.SignInAsync(user.Email!);
        var callerKey = (await this.RemainingKeysAsync(user.Id)).Single(key => key != otherRow.SessionKey);
        var cache = new RemovalRefusingTicketCache(this.App.Services.GetRequiredService<ITicketCacheService>(), otherRow.SessionKey);
        var participant = new RecordingParticipant();
        await using var services = this.BuildLifecycleServices(cache, participant, CallerRequest(user.Id, callerKey));

        // Act
        await Should.ThrowAsync<AggregateException>(() => RunIntentAsync(services, lifecycle => lifecycle.RevokeMySessionAsync(otherRow.Id)));

        // Assert — the participants heard about the session on the failing attempt, and its row is still there...
        participant.Revocations.ShouldHaveSingleItem().SessionId.ShouldBe(otherRow.Id);
        (await this.RemainingKeysAsync(user.Id)).ShouldContain(otherRow.SessionKey);

        // ...so retrying the intent finds the session and ends it.
        cache.Refuse = false;
        var revoked = false;
        await RunIntentAsync(services, async lifecycle => revoked = await lifecycle.RevokeMySessionAsync(otherRow.Id));
        revoked.ShouldBeTrue();
        await ShouldAuthenticateAsync(otherDevice, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(callerDevice, HttpStatusCode.OK);
        participant.Revocations.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RevokeMySession_TheCallersOwnSessionWhoseTicketCannotBeRemoved_KeepsItUntilARetryEndsItAsync()
    {
        // Arrange — the caller ends its own session, and the cache refuses to remove its ticket until it recovers. The
        // caller's sign-out goes through the ticket store, as the cookie handler's does.
        var user = await this.CreateTestUserAsync("revoke_my_own_retry_ticket_test@test.local", SeedData.TenantAId);
        using var callerDevice = await this.SignInAsync(user.Email!);
        var callerRow = await this.GetSingleSessionAsync(user.Id);
        var cache = new RemovalRefusingTicketCache(this.App.Services.GetRequiredService<ITicketCacheService>(), callerRow.SessionKey);
        var participant = new RecordingParticipant();
        ServiceProvider? lifecycleServices = null;
        var signOuts = 0;
        var caller = CallerRequest(user.Id, callerRow.SessionKey, () =>
        {
            signOuts++;
            return lifecycleServices!.GetRequiredService<ITicketStore>().RemoveAsync(callerRow.SessionKey);
        });
        await using var services = this.BuildLifecycleServices(cache, participant, caller);
        lifecycleServices = services;

        // Act — two attempts while the cache keeps refusing.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Should.ThrowAsync<AggregateException>(() => RunIntentAsync(services, lifecycle => lifecycle.RevokeMySessionAsync(callerRow.Id)));
        }

        // Assert — the session is still tracked and the caller still signed in...
        signOuts.ShouldBe(0);
        (await this.RemainingKeysAsync(user.Id)).ShouldBe([callerRow.SessionKey]);
        await ShouldAuthenticateAsync(callerDevice, HttpStatusCode.OK);

        // ...so the retry once the cache has recovered ends the session and signs the caller out.
        cache.Refuse = false;
        var revoked = false;
        await RunIntentAsync(services, async lifecycle => revoked = await lifecycle.RevokeMySessionAsync(callerRow.Id));
        revoked.ShouldBeTrue();
        signOuts.ShouldBe(1);
        (await this.RemainingKeysAsync(user.Id)).ShouldBeEmpty();
        await ShouldAuthenticateAsync(callerDevice, HttpStatusCode.Unauthorized);
        participant.Revocations.Count.ShouldBe(3);
    }

    private static async Task RunIntentAsync(ServiceProvider services, Func<ISessionLifecycle, Task> intent)
    {
        await using var scope = services.CreateAsyncScope();
        await intent(scope.ServiceProvider.GetRequiredService<ISessionLifecycle>());
    }

    /// <summary>
    /// Creates a request that authenticated with the given session, as the cookie handler would report it, and whose
    /// sign-out runs <paramref name="signOut"/>.
    /// </summary>
    private static DefaultHttpContext CallerRequest(Guid accountId, string sessionKey, Func<Task>? signOut = null)
    {
        var properties = new AuthenticationProperties();
        properties.Items[SessionConstants.SessionKeyPropertyName] = sessionKey;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())],
            IdentityConstants.ApplicationScheme));

        var authentication = new Mock<IAuthenticationService>();
        authentication
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, IdentityConstants.ApplicationScheme)));
        authentication
            .Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme, It.IsAny<AuthenticationProperties?>()))
            .Returns(() => signOut?.Invoke() ?? Task.CompletedTask);

        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider(),
        };
    }

    /// <summary>
    /// Composes the session lifecycle the way an application registers it, over the host's database, with the given
    /// ticket cache and participant. The account services the revoking intents never call are stubs.
    /// </summary>
    private ServiceProvider BuildLifecycleServices(ITicketCacheService ticketCache, ISessionRevocationParticipant participant, HttpContext? caller = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>());
        services.AddSingleton(ticketCache);
        services.AddSingleton<IHttpContextAccessor>(new FixedHttpContextAccessor { HttpContext = caller });
        services.AddSingleton(participant);
        services.AddScoped(_ => new Mock<UserManager<TestUser>>(Mock.Of<IUserStore<TestUser>>(), null!, null!, null!, null!, null!, null!, null!, null!).Object);
        services.AddScoped(_ => Mock.Of<ITenantAuthorizationResolver>());
        services.AddScoped(_ => Mock.Of<ITenantMembershipChecker>());
        services.AddBlueprintSessionInfrastructure<TestUser, TestDbContext>();

        return services.BuildServiceProvider();
    }

    private async Task<List<string>> RemainingKeysAsync(Guid userId)
    {
        using var scope = this.App.CreateDbContextScope();
        return await scope.Context.ActiveSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.SessionKey)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Forwards to the host's ticket cache, except that it refuses to remove one session's ticket while
    /// <see cref="Refuse"/> is set.
    /// </summary>
    private sealed class RemovalRefusingTicketCache(ITicketCacheService inner, string refusedKey) : ITicketCacheService
    {
        public bool Refuse { get; set; } = true;

        public Task<AuthenticationTicket?> GetTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
            => inner.GetTicketAsync(sessionKey, cancellationToken);

        public Task SetTicketAsync(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
            => inner.SetTicketAsync(sessionKey, ticket, expiresUtc, cancellationToken);

        public Task RemoveTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
            => this.Refuse && string.Equals(sessionKey, refusedKey, StringComparison.Ordinal)
                ? Task.FromException(new InvalidOperationException("The cache refused the removal."))
                : inner.RemoveTicketAsync(sessionKey, cancellationToken);
    }

    /// <summary>
    /// An accessor that always answers with the same request. The framework's own accessor shares one ambient slot
    /// with the host, whose requests would overwrite it.
    /// </summary>
    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
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
}
