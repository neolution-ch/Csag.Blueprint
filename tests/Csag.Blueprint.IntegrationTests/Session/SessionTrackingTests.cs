namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using System.Net;
using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Logout;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Integration tests for session tracking against the real SQL container: the ticket store records every session it
/// stores, with the signing-in request's details; a sign-in over a live session cookie attributes that session to
/// the signing-in account; renewal and removal keep the row in step with the ticket; and the
/// <see cref="IActiveSessionTracker"/> writes that rely on set-based statements and the unique key index.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SessionTrackingTests(AppFixture app) : SessionTestBase(app)
{
    // Mirrors SessionConstants.SessionKeyMaxCacheKeyBytes: the cache composes the ticket key as
    // "CacheId:AuthTicket_" + Uri.EscapeDataString(sessionKey) and refuses a generated key over 250 UTF-8
    // bytes, so the 19-byte prefix leaves 231 bytes for the encoded session key. The budget itself is pinned
    // against the library by DistributedCache_AcceptsAnAuthTicketKeyAtTheBudgetAndRejectsOneByteMoreAsync, for
    // the cache's default key options: an application configuring an environment prefix or a schema version
    // lengthens the generated key and lowers its own effective ceiling below this one.
    private const int MaxSessionKeyBytes = 231;

    private const int UserAgentMaxLength = 500;

    /// <summary>
    /// The instant the fake clock in the clock-seam tests below is parked at, far from the wall clock so a regression
    /// to <c>DateTimeOffset.UtcNow</c> is unmistakable rather than a near miss.
    /// </summary>
    private static readonly DateTimeOffset ClockEpoch = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SignIn_TracksTheSessionWithTheRequestsDetailsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the request comes through an edge that stamps the client address header the host trusts.
        var user = await this.CreateTestUserAsync("tracking_details_test@test.local", SeedData.TenantAId);
        using var client = this.App.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("integration-agent/1.0");
        client.DefaultRequestHeaders.Add("X-Client-IP", "203.0.113.9");
        var before = DateTimeOffset.UtcNow;

        // Act
        await this.SignInWithAsync(client, user.Email!);

        // Assert — one row, attributed to the user and tenant, with the edge-stamped address.
        var session = await this.GetSingleSessionAsync(user.Id);
        session.CurrentTenantId.ShouldBe(SeedData.TenantAId);
        session.UserAgent.ShouldBe("integration-agent/1.0");
        session.IpAddress.ShouldBe("203.0.113.9");
        session.CreatedAt.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-1));

        // The ticket carries its own key, and row and ticket expire together.
        var ticket = await this.App.Services.GetRequiredService<ITicketCacheService>().GetTicketAsync(session.SessionKey, ct);
        ticket.ShouldNotBeNull();
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName].ShouldBe(session.SessionKey);
        ticket.Properties.ExpiresUtc.ShouldNotBeNull().ShouldBe(session.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SignInOverALiveSessionCookie_ForAnotherAccount_AttributesTheSessionToThatAccountAsync()
    {
        // Arrange — one browser, signed in as the first account, signs in as a second without signing out. The cookie
        // handler reuses the live cookie's session key for the new sign-in.
        var first = await this.CreateTestUserAsync("reattribute_first_test@test.local", SeedData.TenantAId);
        var second = await this.CreateTestUserAsync("reattribute_second_test@test.local", SeedData.TenantBId);
        using var client = await this.SignInAsync(first.Email!);
        var original = await this.GetSingleSessionAsync(first.Id);
        client.DefaultRequestHeaders.Add("X-Client-IP", "198.51.100.23");

        // Act
        await this.SignInWithAsync(client, second.Email!);

        // Assert — the row under that key now describes the second account and its sign-in.
        using var scope = this.App.CreateDbContextScope();
        var rows = await scope.Context.ActiveSessions
            .AsNoTracking()
            .Where(s => s.SessionKey == original.SessionKey)
            .ToListAsync(TestContext.Current.CancellationToken);
        var session = rows.ShouldHaveSingleItem();
        session.UserId.ShouldBe(second.Id);
        session.CurrentTenantId.ShouldBe(SeedData.TenantBId);
        session.IpAddress.ShouldBe("198.51.100.23");
        session.CreatedAt.ShouldBeGreaterThanOrEqualTo(original.CreatedAt);
        (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == first.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SignInOverALiveSessionCookie_RevokingTheNewAccount_EndsTheSessionAsync()
    {
        var first = await this.CreateTestUserAsync("reattribute_revoke_new_first@test.local", SeedData.TenantAId);
        var second = await this.CreateTestUserAsync("reattribute_revoke_new_second@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(first.Email!);
        await this.SignInWithAsync(client, second.Email!);

        await this.RunIntentAsync(lifecycle => lifecycle.AccessRevokedAsync(second.Id));

        await ShouldAuthenticateAsync(client, HttpStatusCode.Unauthorized, "revoking the account now signed in must end the session");
    }

    [Fact]
    public async Task SignInOverALiveSessionCookie_RevokingThePreviousAccount_LeavesTheSessionAsync()
    {
        var first = await this.CreateTestUserAsync("reattribute_revoke_old_first@test.local", SeedData.TenantAId);
        var second = await this.CreateTestUserAsync("reattribute_revoke_old_second@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(first.Email!);
        await this.SignInWithAsync(client, second.Email!);

        await this.RunIntentAsync(lifecycle => lifecycle.AccessRevokedAsync(first.Id));

        await ShouldAuthenticateAsync(client, HttpStatusCode.OK, "the session belongs to the account signed in last");
    }

    [Fact]
    public async Task SignInOverOwnLiveSession_KeepsOneRowAndFollowsTheNewTenantAsync()
    {
        var user = await this.CreateTestUserAsync("resign_same_user_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var client = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        var original = await this.GetSingleSessionAsync(user.Id);

        await this.SignInWithAsync(client, user.Email!, SeedData.TenantBId);

        var session = await this.GetSingleSessionAsync(user.Id);
        session.SessionKey.ShouldBe(original.SessionKey);
        session.CurrentTenantId.ShouldBe(SeedData.TenantBId);
        session.CreatedAt.ShouldBe(original.CreatedAt);
    }

    [Fact]
    public async Task Logout_RemovesTrackedSessionRowAsync()
    {
        // Untracking happens in DistributedCacheTicketStore.RemoveAsync, which the cookie handler calls with the
        // session key just before raising OnSigningOut, so logout clears the row with no event wired up.
        var ct = TestContext.Current.CancellationToken;
        var user = await this.CreateTestUserAsync("logout_untrack_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);

        var logoutUri = new Uri(IEndpoint.TestURLFor<LogoutEndpoint>(), UriKind.Relative);
        var logoutRsp = await client.PostAsync(logoutUri, content: null, ct);
        await logoutRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "logout should succeed", ct);

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.CountAsync(s => s.UserId == user.Id, ct))
                .ShouldBe(0, "logout must remove the tracked session row");
        }

        await ShouldAuthenticateAsync(client, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenewTicket_MovesTheTrackedSessionsExpiryAndTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — track a session row directly, then build the renewed ticket the cookie handler would pass to
        // ITicketStore.RenewAsync on sliding renewal.
        var userId = Guid.NewGuid();
        var sessionKey = NewKey();
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();
        await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, userId, null, DateTimeOffset.UtcNow.AddMinutes(30), "test-agent", "127.0.0.1"), ct);

        var renewedExpiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var ticket = CreateTicket(userId, SeedData.TenantBId, renewedExpiresAt);

        // Act
        await this.App.Services.GetRequiredService<ITicketStore>().RenewAsync(sessionKey, ticket, ct);

        // Assert — the row follows the renewed ticket, so listing, revocation and refresh keep seeing the session.
        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions.AsNoTracking().SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.ExpiresAt.ShouldBe(renewedExpiresAt, TimeSpan.FromSeconds(1));
        session.CurrentTenantId.ShouldBe(SeedData.TenantBId);
        session.UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task TrackAsync_UnderAKeyAlreadyTrackedForAnotherAccount_AttributesTheRowToTheNewAccountAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the unique index on SessionKey makes a blind insert under a tracked key fail.
        var sessionKey = NewKey();
        var clock = new FakeTimeProvider(ClockEpoch);
        var tracker = this.CreateTrackerWithClock(clock);
        var previousAccount = Guid.NewGuid();
        var newAccount = Guid.NewGuid();
        await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, previousAccount, SeedData.TenantAId, ClockEpoch.AddHours(1), "old-agent", "10.0.0.1"), ct);
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, newAccount, SeedData.TenantBId, ClockEpoch.AddHours(2), "new-agent", "10.0.0.2"), ct);

        // Assert
        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions.AsNoTracking().SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.UserId.ShouldBe(newAccount);
        session.CurrentTenantId.ShouldBe(SeedData.TenantBId);
        session.ExpiresAt.ShouldBe(ClockEpoch.AddHours(2));
        session.UserAgent.ShouldBe("new-agent");
        session.IpAddress.ShouldBe("10.0.0.2");
        session.CreatedAt.ShouldBe(ClockEpoch.AddMinutes(5));
    }

    [Fact]
    public async Task RenewAsync_ForTheSameAccount_KeepsTheSignInDetailsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionKey = NewKey();
        var userId = Guid.NewGuid();
        var tracker = this.CreateTrackerWithClock(new FakeTimeProvider(ClockEpoch));
        await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, userId, null, ClockEpoch.AddHours(1), "sign-in-agent", "10.0.0.1"), ct);

        (await tracker.RenewAsync(new SessionTrackingEntry(sessionKey, userId, null, ClockEpoch.AddHours(3), "other-agent", "10.0.0.9"), ct)).ShouldBeTrue();

        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions.AsNoTracking().SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.ExpiresAt.ShouldBe(ClockEpoch.AddHours(3));
        session.UserAgent.ShouldBe("sign-in-agent");
        session.IpAddress.ShouldBe("10.0.0.1");
        session.CreatedAt.ShouldBe(ClockEpoch);
    }

    [Fact]
    public async Task RenewAsync_WithUnknownSessionKey_ReturnsFalseAndCreatesNothingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // "No row" is the tracker's signal to the ticket store that the session was revoked while a renewal was in
        // flight, so it must be reported truthfully, and a renewal must never bring a revoked row back.
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();
        var unknownKey = NewKey();

        (await tracker.RenewAsync(new SessionTrackingEntry(unknownKey, Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(1), null, null), ct)).ShouldBeFalse();

        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ActiveSessions.AnyAsync(s => s.SessionKey == unknownKey, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task UntrackAsync_RemovesRowAndReportsOutcomeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await this.CreateTestUserAsync("untrack_tracker_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);
        var sessionKey = (await this.GetSingleSessionAsync(user.Id)).SessionKey;
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();

        (await tracker.UntrackAsync(sessionKey, ct)).ShouldBeTrue();

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.CountAsync(s => s.SessionKey == sessionKey, ct)).ShouldBe(0);
        }

        // Untracking must only ever remove an existing row, never report success for an unknown key - keys belonging
        // to other cookie schemes simply match nothing.
        (await tracker.UntrackAsync(sessionKey, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task TrackAsync_LeavesThePrimaryKeyToTheSequentialStoreDefaultAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — BlueprintActiveSessionConfiguration gives Id a NEWSEQUENTIALID() store default so the
        // clustered key stays monotonic on this insert-heavy table. Assigning the key client-side would silently
        // defeat that, and two rows are what makes the omission observable: EF only falls back to the store
        // default while the property holds the CLR default, so a client value of Guid.Empty would collide on the
        // second insert instead of being generated per row.
        var firstKey = NewKey();
        var secondKey = NewKey();
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();

        // Act
        foreach (var sessionKey in new[] { firstKey, secondKey })
        {
            await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1"), ct);
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
    public async Task TrackAsync_WithOverLongUserAgentAndIpAddress_ClampsThemAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the User-Agent value is a client-supplied request header bounded only by the server's
        // header limits, and both values are written straight into nvarchar columns. Unclamped, this write fails
        // with a SQL truncation error, and the sign-in that raised it fails over a field that is only displayed.
        var sessionKey = NewKey();
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();

        // Act
        await tracker.TrackAsync(
            new SessionTrackingEntry(sessionKey, Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(1), new string('a', 900), new string('1', 200)),
            ct);

        // Assert — the row exists and carries both values clamped to the mapped column lengths.
        using var dbScope = this.App.CreateDbContextScope();
        var session = await dbScope.Context.ActiveSessions.AsNoTracking().SingleAsync(s => s.SessionKey == sessionKey, ct);
        session.UserAgent.ShouldNotBeNull().Length.ShouldBe(UserAgentMaxLength);
        session.IpAddress.ShouldNotBeNull().Length.ShouldBe(50);
    }

    [Fact]
    public async Task TrackAsync_WithUserAgentStraddlingTheClampBoundary_StoresNoLoneSurrogateAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an emoji positioned so the clamp boundary falls between its two surrogates. Cutting there
        // would store half a character: valid nvarchar, but not valid UTF-16 text, so it fails or is mangled
        // wherever the session listing is serialized back out.
        var sessionKey = NewKey();
        var userAgent = new string('a', UserAgentMaxLength - 1) + "\U0001F600" + new string('b', 400);
        char.IsHighSurrogate(userAgent[UserAgentMaxLength - 1]).ShouldBeTrue();
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();

        // Act
        await tracker.TrackAsync(new SessionTrackingEntry(sessionKey, Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(1), userAgent, "127.0.0.1"), ct);

        // Assert — the clamp stops short of the pair rather than splitting it.
        using var dbScope = this.App.CreateDbContextScope();
        var stored = (await dbScope.Context.ActiveSessions.AsNoTracking().SingleAsync(s => s.SessionKey == sessionKey, ct)).UserAgent.ShouldNotBeNull();
        stored.Length.ShouldBe(UserAgentMaxLength - 1);
        stored.ShouldNotContain(c => char.IsSurrogate(c));
    }

    [Fact]
    public async Task TrackAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsAndWritesNothingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a key on the character budget but carrying the base64 characters outside the URI unreserved
        // set, each of which costs three bytes once encoded. The key fits the 500-character column, so only a
        // byte-measured guard keeps it out: a committed row under a key the cache cannot compose can never have its
        // ticket read, rewritten or removed, and it aborts revocation and refresh for the user's other sessions.
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        var tracker = this.App.Services.GetRequiredService<IActiveSessionTracker>();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await tracker.TrackAsync(
            new SessionTrackingEntry(sessionKey, Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1"), ct));
        exception.ParamName.ShouldBe("entry.SessionKey");

        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ActiveSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
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

    private static string NewKey() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private static AuthenticationTicket CreateTicket(Guid userId, Guid? tenantId, DateTimeOffset expiresAt)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D", CultureInfo.InvariantCulture)));
        if (tenantId.HasValue)
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.TenantId, tenantId.Value.ToString("D", CultureInfo.InvariantCulture)));
        }

        return new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expiresAt },
            IdentityConstants.ApplicationScheme);
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
        var unique = NewKey();
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
    /// Builds an <see cref="ActiveSessionTracker{TContext}"/> on the host's database but with the given clock, so the
    /// time seam can be driven without repointing the shared host's own <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="clock">The clock the tracker should read.</param>
    /// <returns>The tracker under test.</returns>
    private ActiveSessionTracker<TestDbContext> CreateTrackerWithClock(TimeProvider clock)
        => new(this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(), clock);
}
