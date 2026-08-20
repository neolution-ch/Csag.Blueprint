namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using System.Net;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service-level integration tests for the session lifecycle against the real SQL container:
/// <see cref="ISessionManager"/> revocation and listing semantics (which rely on set-based
/// ExecuteDelete), the sliding-renewal path keeping the tracked session row's expiration in step
/// with the renewed ticket, and the <see cref="ISessionExpirationExtender"/> row update.
/// Every test operates on freshly created users so revocation never touches the fixture's
/// pre-authenticated clients: their tracked rows are restored by the per-test snapshot, but their
/// cached tickets live in the host's memory cache and would be gone for the rest of the run.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SessionManagerTests(AppFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// Authenticated-only endpoint without a permission policy, used to probe whether a client's
    /// session still authenticates (200) or has been revoked (401).
    /// </summary>
    private static readonly Uri AuthProbeUri = new("/api/maintenance-records", UriKind.Relative);

    [Fact]
    public async Task RevokeUserSessionsAsync_RemovesAllUserSessionsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — one user signed in on two "devices".
        var user = await this.CreateTestUserAsync("revoke_all_test@test.local", SeedData.TenantAId);
        using var client1 = await this.SignInAsync(user.Email!);
        using var client2 = await this.SignInAsync(user.Email!);

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.CountAsync(s => s.UserId == user.Id, ct)).ShouldBe(2);
        }

        // Act — revoke every session of the user.
        int revokedCount;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            revokedCount = await sessionManager.RevokeUserSessionsAsync(user.Id, ct);
        }

        // Assert — both tracking rows are gone and both cookies stopped authenticating, proving
        // the cached tickets were dropped alongside the rows.
        revokedCount.ShouldBe(2);

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == user.Id, ct)).ShouldBeFalse();
        }

        var rsp1 = await client1.GetAsync(AuthProbeUri, ct);
        var rsp2 = await client2.GetAsync(AuthProbeUri, ct);
        await rsp1.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
        await rsp2.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
    }

    [Fact]
    public async Task RevokeUserSessionsAsync_ScopedToTenant_LeavesOtherTenantSessionsIntactAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a shared account signed in to two tenants at once.
        var user = await this.CreateTestUserAsync("revoke_tenant_scope_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var tenantAClient = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        using var tenantBClient = await this.SignInAsync(user.Email!, SeedData.TenantBId);

        // Act — revoke only the sessions the user holds in tenant A.
        int revokedCount;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            revokedCount = await sessionManager.RevokeUserSessionsAsync(user.Id, SeedData.TenantAId, ct);
        }

        // Assert — exactly the tenant-A session was revoked; the tenant-B session keeps its
        // tracking row and its cached ticket.
        revokedCount.ShouldBe(1);

        using (var scope = this.App.CreateDbContextScope())
        {
            var remaining = await scope.Context.ActiveSessions
                .Where(s => s.UserId == user.Id)
                .ToListAsync(ct);
            remaining.ShouldHaveSingleItem().CurrentTenantId.ShouldBe(SeedData.TenantBId);
        }

        var tenantARsp = await tenantAClient.GetAsync(AuthProbeUri, ct);
        var tenantBRsp = await tenantBClient.GetAsync(AuthProbeUri, ct);
        await tenantARsp.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
        await tenantBRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "sessions scoped to other tenants must survive a tenant-scoped revocation", ct);
    }

    [Fact]
    public async Task RevokeOtherUserSessionsAsync_RevokesAllButTheKeptSessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the same user signed in on three "devices".
        var user = await this.CreateTestUserAsync("revoke_others_test@test.local", SeedData.TenantAId);
        using var keptClient = await this.SignInAsync(user.Email!);

        // Capture the kept client's session key while its row is the only one, so the key is
        // unambiguously mapped to that client (all three sessions are otherwise identical).
        string keptSessionKey;
        using (var scope = this.App.CreateDbContextScope())
        {
            var kept = await scope.Context.ActiveSessions.SingleAsync(s => s.UserId == user.Id, ct);
            keptSessionKey = kept.SessionKey;
        }

        using var otherClient1 = await this.SignInAsync(user.Email!);
        using var otherClient2 = await this.SignInAsync(user.Email!);

        // Act — revoke every session for the user EXCEPT the kept one.
        int revokedCount;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            revokedCount = await sessionManager.RevokeOtherUserSessionsAsync(user.Id, keptSessionKey, ct);
        }

        // Assert — the two other sessions were revoked; the kept one preserved.
        revokedCount.ShouldBe(2);

        using (var scope = this.App.CreateDbContextScope())
        {
            var remaining = await scope.Context.ActiveSessions
                .Where(s => s.UserId == user.Id)
                .Select(s => s.SessionKey)
                .ToListAsync(ct);
            remaining.ShouldHaveSingleItem().ShouldBe(keptSessionKey);
        }

        // Assert — the kept ticket survives in the cache, so the kept client stays authenticated
        // while the other two get 401 on their next request.
        var ticketCache = this.App.Services.GetRequiredService<ITicketCacheService>();
        (await ticketCache.GetTicketAsync(keptSessionKey, ct)).ShouldNotBeNull("the preserved session's ticket must remain in the cache");

        var keptRsp = await keptClient.GetAsync(AuthProbeUri, ct);
        var other1Rsp = await otherClient1.GetAsync(AuthProbeUri, ct);
        var other2Rsp = await otherClient2.GetAsync(AuthProbeUri, ct);
        await keptRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "the preserved session must stay authenticated", ct);
        await other1Rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
        await other2Rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RevokeOtherUserSessionsAsync_WithMissingKeepKey_ThrowsAsync(string? keepSessionKey)
    {
        var ct = TestContext.Current.CancellationToken;

        using var serviceScope = this.App.Services.CreateScope();
        var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();

        // A null OR empty keep-key would degrade the filter to "revoke everything" and silently
        // sign the caller out too; the API rejects both (ArgumentException.ThrowIfNullOrEmpty) so
        // revoking all sessions is always a deliberate, separate call. ArgumentNullException
        // derives from ArgumentException, so a single assertion covers both inline cases.
        await Should.ThrowAsync<ArgumentException>(async () =>
            await sessionManager.RevokeOtherUserSessionsAsync(Guid.NewGuid(), keepSessionKey!, ct));
    }

    [Fact]
    public async Task RevokeSessionAsync_RemovesSingleSessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a signed-in user whose session works.
        var user = await this.CreateTestUserAsync("revoke_single_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);

        string sessionKey;
        using (var scope = this.App.CreateDbContextScope())
        {
            var activeSession = await scope.Context.ActiveSessions.SingleAsync(s => s.UserId == user.Id, ct);
            sessionKey = activeSession.SessionKey;
        }

        var beforeRsp = await client.GetAsync(AuthProbeUri, ct);
        await beforeRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        // Act — revoke that single session by its key.
        bool revoked;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            revoked = await sessionManager.RevokeSessionAsync(sessionKey, ct);
        }

        revoked.ShouldBeTrue();

        // Assert — the cached ticket is dropped, so the cookie (which only carries the session
        // key) no longer resolves to a principal and the very next request is 401.
        var ticketCache = this.App.Services.GetRequiredService<ITicketCacheService>();
        (await ticketCache.GetTicketAsync(sessionKey, ct)).ShouldBeNull("revocation must drop the cached ticket");

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
        }

        var afterRsp = await client.GetAsync(AuthProbeUri, ct);
        await afterRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);

        // Revoking the same key again finds nothing to remove.
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            (await sessionManager.RevokeSessionAsync(sessionKey, ct)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetUserSessionsAsync_ReturnsBothSessionsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — one user signed in twice.
        var user = await this.CreateTestUserAsync("get_sessions_test@test.local", SeedData.TenantAId);
        using var client1 = await this.SignInAsync(user.Email!);
        using var client2 = await this.SignInAsync(user.Email!);

        // Act — list the user's active sessions.
        List<ActiveSessionInfo> sessions;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            sessions = await sessionManager.GetUserSessionsAsync(user.Id, ct);
        }

        // Assert — both sessions are returned with plausible tracking metadata and the tenant the
        // sign-ins were scoped to.
        sessions.Count.ShouldBe(2);
        foreach (var session in sessions)
        {
            session.SessionKey.ShouldNotBeNullOrEmpty();
            session.CreatedAt.ShouldBeLessThan(DateTimeOffset.UtcNow.AddSeconds(5));
            session.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
            session.CurrentTenantId.ShouldBe(SeedData.TenantAId);
        }
    }

    [Fact]
    public async Task RenewTicket_ExtendsTrackedSessionExpirationAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — track a session row directly, then build the renewed ticket the cookie
        // handler would pass to ITicketStore.RenewAsync on sliding renewal.
        var userId = Guid.NewGuid();
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var initialExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        using var serviceScope = this.App.Services.CreateScope();
        var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
        var ticketStore = this.App.Services.GetRequiredService<ITicketStore>();

        await sessionManager.TrackSessionAsync(
            userId,
            sessionKey,
            initialExpiresAt,
            "test-agent",
            "127.0.0.1",
            currentTenantId: null,
            ct);

        var renewedExpiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D", CultureInfo.InvariantCulture)));
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = renewedExpiresAt },
            IdentityConstants.ApplicationScheme);

        // Act — renew the ticket (what the cookie handler does on sliding renewal).
        await ticketStore.RenewAsync(sessionKey, ticket, ct);

        // Assert — the tracked session row's ExpiresAt follows the renewed ticket, so session
        // listing, revocation, and refresh keep seeing long-lived active sessions.
        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions
            .AsNoTracking()
            .SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.ExpiresAt.ShouldBe(renewedExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SessionExpirationExtender_UpdatesTrackedRowWithoutLoadingItAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a tracked session row.
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            await sessionManager.TrackSessionAsync(
                Guid.NewGuid(),
                sessionKey,
                DateTimeOffset.UtcNow.AddMinutes(30),
                "test-agent",
                "127.0.0.1",
                currentTenantId: null,
                ct);
        }

        // Act — extend via the singleton extender, which issues a set-based ExecuteUpdate against
        // the key (no entity load, safe to call from the singleton ticket store).
        var extender = this.App.Services.GetRequiredService<ISessionExpirationExtender>();
        var newExpiresAt = DateTimeOffset.UtcNow.AddHours(12);
        var extended = await extender.ExtendAsync(sessionKey, newExpiresAt, ct);

        // Assert — the row reports as updated and carries the new expiration.
        extended.ShouldBeTrue();

        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions
            .AsNoTracking()
            .SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.ExpiresAt.ShouldBe(newExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SessionExpirationExtender_WithUnknownSessionKey_ReturnsFalseAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // "No row" is the extender's signal to the ticket store that the session was revoked while
        // a renewal was in flight, so it must be reported truthfully rather than swallowed.
        var extender = this.App.Services.GetRequiredService<ISessionExpirationExtender>();
        var unknownKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        (await extender.ExtendAsync(unknownKey, DateTimeOffset.UtcNow.AddHours(1), ct)).ShouldBeFalse();
    }

    /// <summary>
    /// Creates a fresh user with the shared default password and makes it a member of the given
    /// tenants (in the given order, so the first tenant becomes the sign-in default).
    /// </summary>
    /// <param name="email">The new user's email (also used as user name).</param>
    /// <param name="tenantIds">Tenants to create memberships for.</param>
    /// <returns>The created user.</returns>
    private async Task<TestUser> CreateTestUserAsync(string email, params Guid[] tenantIds)
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = new TestUser { Email = email, UserName = email };
        var createResult = await userManager.CreateAsync(user, SeedData.DefaultPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        foreach (var tenantId in tenantIds)
        {
            context.TenantMemberships.Add(new BlueprintTenantMembership<TestUser, TestTenant>
            {
                UserId = user.Id,
                TenantId = tenantId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }

        await context.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Signs the given user in on a brand-new client (its own cookie container), tracking an
    /// active session scoped to the resolved tenant.
    /// </summary>
    /// <param name="email">The user's email; the password is the shared <see cref="SeedData.DefaultPassword"/>.</param>
    /// <param name="tenantId">Tenant to scope the session to; defaults to the user's first membership.</param>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> SignInAsync(string email, Guid? tenantId = null)
    {
        var client = this.App.CreateClient();
        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = email,
                Password = SeedData.DefaultPassword,
                TenantId = tenantId,
            });

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, $"failed to authenticate test user '{email}'");
        AppFixture.SetCsrfHeaderFromResponse(client, rsp);
        return client;
    }
}
