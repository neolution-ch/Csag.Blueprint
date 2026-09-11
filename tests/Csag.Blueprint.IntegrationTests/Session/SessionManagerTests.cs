namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using System.Net;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Enums;
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
using Neolution.Extensions.Caching.Abstractions;

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
    // Mirrors SessionKeyMaxCacheKeyBytes in SessionManager: the cache composes the ticket key as
    // "CacheId:AuthTicket_" + Uri.EscapeDataString(sessionKey) and refuses a generated key over 250 UTF-8
    // bytes, so the 19-byte prefix leaves 231 bytes for the encoded session key. The budget itself is pinned
    // against the library by DistributedCache_AcceptsAnAuthTicketKeyAtTheBudgetAndRejectsOneByteMoreAsync, for
    // the cache's default key options: an application configuring an environment prefix or a schema version
    // lengthens the generated key and lowers its own effective ceiling below this one.
    private const int MaxSessionKeyBytes = 231;

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
    public async Task TrackSessionAsync_LeavesThePrimaryKeyToTheSequentialStoreDefaultAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — BlueprintActiveSessionConfiguration gives Id a NEWSEQUENTIALID() store default so the
        // clustered key stays monotonic on this insert-heavy table. Assigning the key client-side would silently
        // defeat that, and two rows are what makes the omission observable: EF only falls back to the store
        // default while the property holds the CLR default, so a client value of Guid.Empty would collide on the
        // second insert instead of being generated per row.
        var firstKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var secondKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        using var serviceScope = this.App.Services.CreateScope();
        var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();

        // Act
        foreach (var sessionKey in new[] { firstKey, secondKey })
        {
            await sessionManager.TrackSessionAsync(
                Guid.NewGuid(),
                sessionKey,
                DateTimeOffset.UtcNow.AddHours(1),
                "test-agent",
                "127.0.0.1",
                currentTenantId: null,
                ct);
        }

        // Assert — both rows carry a server-generated key.
        using var dbScope = this.App.CreateDbContextScope();
        var ids = await dbScope.Context.ActiveSessions
            .AsNoTracking()
            .Where(s => s.SessionKey == firstKey || s.SessionKey == secondKey)
            .Select(s => s.Id)
            .ToListAsync(ct);

        ids.Count.ShouldBe(2);
        ids.ShouldAllBe(id => id != Guid.Empty);
        ids.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task TrackSessionAsync_WithOverLongUserAgentAndIpAddress_ClampsThemAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the User-Agent value is a client-supplied request header bounded only by the server's
        // header limits, and both values are written straight into nvarchar columns. Unclamped, this insert
        // fails with a SQL truncation error, and the sign-in that raised it keeps a cached ticket with no
        // tracking row.
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        using var serviceScope = this.App.Services.CreateScope();
        var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();

        // Act
        await sessionManager.TrackSessionAsync(
            Guid.NewGuid(),
            sessionKey,
            DateTimeOffset.UtcNow.AddHours(1),
            new string('a', 900),
            new string('1', 200),
            currentTenantId: null,
            ct);

        // Assert — the row exists and carries both values clamped to the mapped column lengths.
        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions
            .AsNoTracking()
            .SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.UserAgent.ShouldNotBeNull().Length.ShouldBe(500);
        session.IpAddress.ShouldNotBeNull().Length.ShouldBe(50);
    }

    [Fact]
    public async Task TrackSessionAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsAndWritesNothingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a key on the character budget but carrying the base64 characters outside the URI
        // unreserved set, each of which costs three bytes once encoded. The key fits the 500-character column,
        // so only a byte-measured guard keeps it out: a committed row under a key the cache cannot compose can
        // never have its ticket read, rewritten or removed, and it aborts revocation and refresh for the user's
        // other sessions once the loop reaches it.
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        using var serviceScope = this.App.Services.CreateScope();
        var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();

        // Act & Assert — nothing on this path consults the cache, so without a byte-measured guard the key
        // would fit the 500-character column and the row would simply commit. The parameter name pins that the
        // manager refused it rather than something downstream.
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await sessionManager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", currentTenantId: null, ct));
        exception.ParamName.ShouldBe("sessionKey");

        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ActiveSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeSessionAsync_WithMalformedSessionKey_ReturnsFalseWithoutRemovingTheSharedCacheSlotAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a live session, plus a sentinel in the key-less slot the cache falls back to for a blank
        // key. The sentinel is what makes the fallback observable: the live session's ticket lives under its
        // own key and would survive a blank-keyed removal regardless.
        var user = await this.CreateTestUserAsync("revoke_malformed_key_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);

        string trackedKey;
        using (var scope = this.App.CreateDbContextScope())
        {
            trackedKey = (await scope.Context.ActiveSessions.SingleAsync(s => s.UserId == user.Id, ct)).SessionKey;
        }

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
        var sharedSlotSentinel = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        await cache.SetWithOptionsAsync(CacheId.AuthTicket, sharedSlotSentinel, options, ct);

        try
        {
            using var serviceScope = this.App.Services.CreateScope();
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();

            // Act — a blank key and an over-budget key are both reported as "nothing to revoke" rather than
            // raising out of the administrative call that supplied them.
            var blankRevoked = await sessionManager.RevokeSessionAsync("   ", ct);
            var overBudgetRevoked = await sessionManager.RevokeSessionAsync(CreateUnreservedKey(MaxSessionKeyBytes + 1), ct);

            // Assert
            blankRevoked.ShouldBeFalse();
            overBudgetRevoked.ShouldBeFalse();

            (await cache.GetAsync<string>(CacheId.AuthTicket, ct))
                .ShouldBe(sharedSlotSentinel, "a blank key must not evict the shared cache slot");

            var ticketCache = this.App.Services.GetRequiredService<ITicketCacheService>();
            (await ticketCache.GetTicketAsync(trackedKey, ct)).ShouldNotBeNull("the live session's ticket must survive");

            var rsp = await client.GetAsync(AuthProbeUri, ct);
            await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "a malformed revoke key must not affect other sessions", ct);
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.AuthTicket, ct);
        }
    }

    [Fact]
    public async Task DistributedCache_AcceptsAnAuthTicketKeyAtTheBudgetAndRejectsOneByteMoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The budget is not an arbitrary number: it is the cache's own 250-byte limit on the generated key
        // minus the 19-byte "CacheId:AuthTicket_" prefix. Pin it against the library so a prefix change made
        // here (a renamed enum member, a configured environment prefix or schema version) fails in this test
        // rather than at runtime, and so the constant is checked by something other than a restatement of
        // itself.
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

        await Should.NotThrowAsync(() => cache.SetWithOptionsAsync(
            CacheId.AuthTicket, CreateUnreservedKey(MaxSessionKeyBytes), "ticket", options, ct));

        await Should.ThrowAsync<ArgumentException>(async () => await cache.SetWithOptionsAsync(
            CacheId.AuthTicket, CreateUnreservedKey(MaxSessionKeyBytes + 1), "ticket", options, ct));
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
    /// Builds a session key of the requested character count from URI-unreserved characters only, so it
    /// percent-encodes to itself and one character costs exactly one byte of the cache-key budget. A fresh Guid
    /// leads the key: cache entries live in the host's memory and outlive the per-test database restore, so a
    /// key reused across tests could be resolved by a ticket no row backs any more.
    /// </summary>
    /// <param name="length">The number of characters in the resulting key.</param>
    /// <returns>The generated session key.</returns>
    private static string CreateUnreservedKey(int length)
    {
        var unique = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return unique + new string('a', length - unique.Length);
    }

    /// <summary>
    /// Builds a session key of the requested character count that ends in the three characters a standard
    /// base64 token can carry that the URI unreserved set does not. Each costs three bytes once percent-encoded,
    /// so the key sits on the character budget but over the byte budget.
    /// </summary>
    /// <param name="length">The number of characters in the resulting key.</param>
    /// <returns>The generated session key.</returns>
    private static string CreateStandardBase64Key(int length)
    {
        var key = CreateUnreservedKey(length).ToCharArray();
        key[^3] = '+';
        key[^2] = '/';
        key[^1] = '=';
        return new string(key);
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
