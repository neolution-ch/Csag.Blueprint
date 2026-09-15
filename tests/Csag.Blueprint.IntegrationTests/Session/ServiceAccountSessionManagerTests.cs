namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Service-level integration tests for <see cref="IServiceAccountSessionManager"/> session-key handling against
/// the real SQL container and the real Neolution cache-key pipeline. The test host substitutes only the inner
/// byte store (a distributed memory cache), so key encoding and the 250-byte limit on the generated key are
/// still enforced by the very library the manager's byte budget is derived from — which is what lets these
/// tests pin that budget instead of restating it, and what lets them show that a key the cache would refuse is
/// never handed to it.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ServiceAccountSessionManagerTests(AppFixture app) : IntegrationTestBase(app)
{
    // Mirrors SessionKeyMaxCacheKeyBytes in ServiceAccountSessionManager: the cache composes its key as
    // "CacheId:ServiceAccountSession_" + Uri.EscapeDataString(sessionKey) and refuses a generated key over 250
    // UTF-8 bytes, so the 30-byte prefix leaves 220 bytes for the encoded session key. The budget itself is
    // pinned against the library by DistributedCache_AcceptsAKeyAtTheBudgetAndRejectsOneByteMoreAsync, for the
    // cache's default key options: an application configuring an environment prefix or a schema version
    // lengthens the generated key and lowers its own effective ceiling below this one.
    private const int MaxSessionKeyBytes = 220;

    /// <summary>
    /// The instant the fake clocks in the expiry and cleanup tests below are parked at. It sits well before any
    /// session the fixture itself tracks, so the table-wide delete can never reach their rows, and far enough
    /// from the wall clock that a filter reading <c>DateTimeOffset.UtcNow</c> instead of its injected
    /// <see cref="TimeProvider"/> is unmistakable rather than a near miss.
    /// </summary>
    private static readonly DateTimeOffset ClockEpoch = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TrackSessionAsync_WithSessionKeyAtTheBudget_TracksRowAndPrimesCacheMarkerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the longest key the cache accepts, exercising the accept path end to end (including the
        // expired-row reap, which needs the relational provider's ExecuteDelete).
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            await manager.TrackSessionAsync(serviceAccountId, sessionKey, expiresAt, "test-agent", "127.0.0.1", ct);
        }

        // Assert — the tracking row carries the key verbatim, and the fast-path marker resolves it to the
        // account, so the next request skips the tracking-row lookup. ValidateSessionAsync still reads the
        // service-account row itself on every request, which is what makes deactivation and role changes
        // take effect immediately rather than being frozen into the token.
        using (var dbScope = this.App.CreateDbContextScope())
        {
            var row = await dbScope.Context.ServiceAccountSessions
                .AsNoTracking()
                .SingleAsync(s => s.SessionKey == sessionKey, ct);
            row.ServiceAccountId.ShouldBe(serviceAccountId);
        }

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
            .ShouldBe(serviceAccountId.ToString("D", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task TrackSessionAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsAndWritesNothingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the same character count as the accepted key above, but carrying the base64 characters
        // outside the URI unreserved set ('+', '/' and the '=' pad), each of which costs three bytes once
        // encoded. Tracking is not transactional: the marker is written only after the row is committed, so a
        // key the cache refuses would leave behind a row that can never be validated or revoked, and that row
        // would in turn abort revocation for every other session on the account.
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();

        // Act & Assert — the cache raises ArgumentException for this key as well, naming its own 'key'
        // parameter, so the parameter name is what shows the key was refused up front rather than on the way
        // into the cache after the row had been written.
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct));
        exception.ParamName.ShouldBe("sessionKey");

        // No row was committed. The marker is not asserted on: reading it would mean asking the cache to
        // compose the very key it refuses, which is the reason the row must not exist in the first place.
        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task DistributedCache_AcceptsAKeyAtTheBudgetAndRejectsOneByteMoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The manager's budget is not an arbitrary number: it is the cache's own 250-byte limit on the
        // generated key minus the 30-byte "CacheId:ServiceAccountSession_" prefix. Pin it against the library
        // so a prefix change made here (a renamed enum member, a configured environment prefix or schema
        // version) fails in this test rather than at runtime, and so the constant is checked by something other
        // than a restatement of itself.
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

        await Should.NotThrowAsync(() => cache.SetWithOptionsAsync(
            CacheId.ServiceAccountSession, CreateUnreservedKey(MaxSessionKeyBytes), "marker", options, ct));

        await Should.ThrowAsync<ArgumentException>(async () => await cache.SetWithOptionsAsync(
            CacheId.ServiceAccountSession, CreateUnreservedKey(MaxSessionKeyBytes + 1), "marker", options, ct));
    }

    [Fact]
    public async Task ValidateSessionAsync_WithSessionKeyOverTheBudget_ReturnsNullInsteadOfThrowingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The session-id claim arrives from the wire, so this key is client-supplied. Handing it to the cache
        // would raise ArgumentException inside the authentication pipeline, turning a request that should be
        // rejected into an unhandled server error; the manager reports "no session" instead.
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();

        (await manager.ValidateSessionAsync(CreateUnreservedKey(MaxSessionKeyBytes + 1), ct)).ShouldBeNull();
        (await manager.ValidateSessionAsync(CreateStandardBase64Key(MaxSessionKeyBytes), ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithBlankSessionKey_DoesNotResolveTheSharedCacheSlotAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an active account whose id sits in the slot the cache falls back to for a blank key. The
        // session-id claim arrives from the wire, so a whitespace claim that reached the cache would read this
        // slot and authenticate as this account without any session ever having been tracked.
        var serviceAccountId = Guid.NewGuid();
        using (var dbScope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            dbScope.Context.ServiceAccounts.Add(new BlueprintServiceAccount
            {
                Id = serviceAccountId,
                TenantId = SeedData.TenantAId,
                Name = "shared slot probe",
                ClientId = $"shared-slot-probe-{serviceAccountId:N}",
                ClientSecretHash = "not-a-real-hash",
                IsActive = true,
            });
            await dbScope.Context.SaveChangesAsync(ct);
        }

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
        await cache.SetWithOptionsAsync(
            CacheId.ServiceAccountSession, serviceAccountId.ToString("D", CultureInfo.InvariantCulture), options, ct);

        try
        {
            using var serviceScope = this.App.Services.CreateScope();
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();

            // Act & Assert
            // A token with no session-id claim reads as null, which the signature accepts rather than forcing
            // callers to suppress nullable analysis or guard ahead of a method documented to report "no session".
            (await manager.ValidateSessionAsync(null, ct)).ShouldBeNull();
            (await manager.ValidateSessionAsync(string.Empty, ct)).ShouldBeNull();
            (await manager.ValidateSessionAsync("   ", ct)).ShouldBeNull();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, ct);
        }
    }

    [Fact]
    public async Task ValidateSessionAsync_WhenTheAccountIsDeactivated_ReturnsNullWithTheSessionStillTrackedAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an active account with a tracked session, validating as itself.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        await this.CreateServiceAccountAsync(serviceAccountId, ["tenant-viewer"], ["vehicles.read"]);

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
        await manager.TrackSessionAsync(
            serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);

        try
        {
            (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldNotBeNull();

            // Act — clear IsActive and leave the session itself alone.
            using (var accountScope = this.App.CreateDbContextScope(SeedData.TenantAId))
            {
                var account = await accountScope.Context.ServiceAccounts.SingleAsync(sa => sa.Id == serviceAccountId, ct);
                account.IsActive = false;
                await accountScope.Context.SaveChangesAsync(ct);
            }

            // Assert — the very next request is rejected while both halves of the session are still in place, so
            // the rejection comes from reading the account's current state rather than from the session having
            // been revoked. Deactivation that only bit once the token expired, or once someone remembered to
            // call RevokeServiceAccountSessionsAsync, would pass a weaker test.
            (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldBeNull();

            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
                .ShouldBe(serviceAccountId.ToString("D", CultureInfo.InvariantCulture));

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeTrue();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task ValidateSessionAsync_WhenTheAccountRowIsDeleted_ReturnsNullOnBothResolutionPathsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an active account with a tracked session.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        await this.CreateServiceAccountAsync(serviceAccountId, ["tenant-viewer"], ["vehicles.read"]);

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
        await manager.TrackSessionAsync(
            serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);

        try
        {
            (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldNotBeNull();

            // Act — remove the account row outright. BlueprintServiceAccount carries no soft-delete flag, so
            // deletion is a real DELETE and "the account is gone" is the same state as "the row was never there".
            using (var accountScope = this.App.CreateDbContextScope(SeedData.TenantAId))
            {
                var account = await accountScope.Context.ServiceAccounts.SingleAsync(sa => sa.Id == serviceAccountId, ct);
                accountScope.Context.ServiceAccounts.Remove(account);
                await accountScope.Context.SaveChangesAsync(ct);
            }

            // Assert — the session outlives the account: BlueprintServiceAccountSession has no foreign key to
            // BlueprintServiceAccounts, so nothing cascades the tracking row away and the marker keeps resolving
            // the key to an id no row answers to. Catching that is the null check's job.
            using (var sessionScope = this.App.CreateDbContextScope())
            {
                (await sessionScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct))
                    .ShouldBeTrue("the tracking row survives the account, so the account lookup is what rejects the request");
            }

            (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldBeNull();

            // The database fallback rejects it too. Dropping the marker sends the next call down the
            // tracking-row path, which resolves the same vanished id instead of reporting "no session", so the
            // null check is reached on both routes into the account read.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
            (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldBeNull();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task ValidateSessionAsync_AfterRolesAndPermissionsChange_ReturnsTheCurrentGrantsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an account holding a read-only grant at the moment the session is issued.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        await this.CreateServiceAccountAsync(serviceAccountId, ["tenant-viewer"], ["vehicles.read"]);

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
        await manager.TrackSessionAsync(
            serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);

        try
        {
            var issued = await manager.ValidateSessionAsync(sessionKey, ct);
            issued.ShouldNotBeNull();
            issued.ServiceAccountId.ShouldBe(serviceAccountId);
            issued.TenantId.ShouldBe(SeedData.TenantAId);
            issued.Roles.ShouldBe(["tenant-viewer"]);
            issued.Permissions.ShouldBe(["vehicles.read"]);

            // Act — re-grant the account without touching the session.
            using (var accountScope = this.App.CreateDbContextScope(SeedData.TenantAId))
            {
                var account = await accountScope.Context.ServiceAccounts.SingleAsync(sa => sa.Id == serviceAccountId, ct);
                account.Roles = ["tenant-manager"];
                account.Permissions = ["vehicles.manage"];
                await accountScope.Context.SaveChangesAsync(ct);
            }

            // The arrange left an ambient tenant behind; drop it so the account read happens the way the
            // authentication pipeline makes it, before TenantMiddleware has resolved one. The tenant filter fails
            // closed with no tenant in scope, so this also pins the IgnoreQueryFilters on that read.
            TenantContext.Clear();

            // Assert — the same key resolves to the new grant on the next call. The marker holds nothing but the
            // account id, so the fast path saves the session lookup and never the authorization read: a marker
            // that cached roles, or a manager that echoed the token's own claims, would still report the set the
            // session was issued with.
            var regranted = await manager.ValidateSessionAsync(sessionKey, ct);
            regranted.ShouldNotBeNull();
            regranted.Roles.ShouldBe(["tenant-manager"]);
            regranted.Permissions.ShouldBe(["vehicles.manage"]);
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task ValidateSessionAsync_WhenTheMarkerIsLostButTheRowIsLive_ResolvesFromTheRowWithoutRePrimingTheMarkerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an active account with a tracked session, then the marker dropped out from under it. That is
        // what a cache eviction or flush leaves behind, and the row is still present and unexpired, so the token
        // has to keep authorizing off the tracking row alone.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        await this.CreateServiceAccountAsync(serviceAccountId, ["tenant-manager"], ["tenants.manage"]);

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
        await manager.TrackSessionAsync(
            serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);

        try
        {
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
                .ShouldBeNull("the database fallback is only under test once the marker is gone");

            // Act
            var validation = await manager.ValidateSessionAsync(sessionKey, ct);

            // Assert — the session still resolves, and the authorization still comes from the account row rather
            // than from anything the lost marker carried.
            validation.ShouldNotBeNull();
            validation.ServiceAccountId.ShouldBe(serviceAccountId);
            validation.TenantId.ShouldBe(SeedData.TenantAId);
            validation.Roles.ShouldHaveSingleItem().ShouldBe("tenant-manager");
            validation.Permissions.ShouldHaveSingleItem().ShouldBe("tenants.manage");

            // The fallback left the marker alone. Only issuance writes markers: a marker re-primed from a row
            // read could land after revocation's final sweep, outliving the row it was read from, and no later
            // revoke could clear it because revocation enumerates rows to find the keys to remove.
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
                .ShouldBeNull("the database fallback must not re-prime the fast-path marker");
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task ValidateSessionAsync_WhenTheMarkerIsLostAndTheRowHasExpired_RejectsAgainstTheInjectedClockAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an active account and a tracking row seeded directly, so no marker is ever written and every
        // lookup below goes through the database fallback. The row is seeded rather than tracked because
        // issuance under this clock would prime a marker whose absolute expiry is already in the past for the
        // host's cache. Id is left unset so the NEWSEQUENTIALID() store default applies, as it does for the rows
        // the manager writes itself.
        var clock = new FakeTimeProvider(ClockEpoch);
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        await this.CreateServiceAccountAsync(serviceAccountId, [], []);

        using (var seedScope = this.App.CreateDbContextScope())
        {
            seedScope.Context.ServiceAccountSessions.Add(new BlueprintServiceAccountSession
            {
                ServiceAccountId = serviceAccountId,
                SessionKey = sessionKey,
                CreatedAt = ClockEpoch,
                ExpiresAt = ClockEpoch.AddMinutes(30),
            });
            await seedScope.Context.SaveChangesAsync(ct);
        }

        var manager = new ServiceAccountSessionManager<TestDbContext>(
            this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(),
            this.App.Services.GetRequiredService<IDistributedCache<CacheId>>(),
            clock);

        // Act & Assert — the account is active and the row is present, so while the clock sits before the expiry
        // the fallback resolves. Establishing that first is what makes the rejection below attributable to the
        // expiry filter rather than to a missing or deactivated account; it is also what pins the filter to the
        // injected clock, since this row is years stale under the wall clock.
        (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldNotBeNull();

        // Park the clock exactly on ExpiresAt. The fallback filters ExpiresAt > now, so the row is refused the
        // moment the clock reaches its expiry rather than a tick later.
        clock.Advance(TimeSpan.FromMinutes(30));
        (await manager.ValidateSessionAsync(sessionKey, ct)).ShouldBeNull();

        // The row is still there: an expired session is inert for authorization because of the filter, not
        // because something deleted it. Nothing in the validation path reaps, and the opportunistic reap only
        // runs on this account's next issuance.
        using var assertScope = this.App.CreateDbContextScope();
        (await assertScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct))
            .ShouldBeTrue("the rejection must come from the expiry filter, not from the row having been reaped");
    }

    [Fact]
    public async Task RevokeSessionAsync_WithTrackedSessionKey_RemovesRowAndMarkerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a tracked session at the budget.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            await manager.TrackSessionAsync(
                serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);
        }

        // Act
        bool revoked;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            revoked = await manager.RevokeSessionAsync(sessionKey, ct);
        }

        // Assert — both halves are gone: the marker closes the validation fast path, the row closes the
        // database fallback. While either remained, an outstanding token would keep authorizing.
        revoked.ShouldBeTrue();

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct)).ShouldBeNull();

        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeSessionAsync_WithAKeySqlMatchesButTheCacheDoesNot_RemovesTheStoredKeysMarkerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The two stores disagree on what makes two keys equal: the cache compares the key it is handed, while
        // the row predicate becomes SQL string equality, which folds case under a case-insensitive column
        // collation and ignores trailing spaces under every SQL Server collation. Revoking under a spelling that
        // matches the row but not the marker must still take the marker out — otherwise the row is deleted, the
        // marker stays live on the validation fast path, and no later revoke can find it, because revocation
        // enumerates rows.
        var serviceAccountId = Guid.NewGuid();
        var storedKey = CreateUnreservedKey(MaxSessionKeyBytes - 1);
        var revokeKey = storedKey + " ";

        await this.CreateServiceAccountAsync(serviceAccountId, ["TenantViewer"], []);

        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            await manager.TrackSessionAsync(
                serviceAccountId, storedKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);
        }

        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        (await cache.GetAsync<string>(CacheId.ServiceAccountSession, storedKey, ct)).ShouldNotBeNull();

        // Act — revoke under the trailing-space spelling, which SQL equality matches and the cache does not.
        bool revoked;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            revoked = await manager.RevokeSessionAsync(revokeKey, ct);
        }

        try
        {
            revoked.ShouldBeTrue("SQL string equality matches the stored row under this spelling");

            // The marker under the key the row actually stored is the one an outstanding token presents.
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, storedKey, ct))
                .ShouldBeNull("the marker for the deleted row must not survive the revoke");

            using (var dbScope = this.App.CreateDbContextScope())
            {
                (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == storedKey, ct))
                    .ShouldBeFalse();
            }

            using var validationScope = this.App.Services.CreateScope();
            var validator = validationScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            (await validator.ValidateSessionAsync(storedKey, ct)).ShouldBeNull();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, storedKey, ct);
            await cache.RemoveAsync(CacheId.ServiceAccountSession, revokeKey, ct);
        }
    }

    [Fact]
    public async Task RevokeSessionAsync_WithMalformedSessionKey_ReturnsFalseWithoutRemovingTheSharedCacheSlotAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a live session, plus a sentinel in the shared slot the cache falls back to for a blank
        // key. The sentinel is what makes the fallback observable: the tracked session's own marker lives
        // under its own key and would survive a blank-keyed removal regardless.
        var serviceAccountId = Guid.NewGuid();
        var trackedKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();
        var options = new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
        var sharedSlotSentinel = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        await cache.SetWithOptionsAsync(CacheId.ServiceAccountSession, sharedSlotSentinel, options, ct);

        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
        await manager.TrackSessionAsync(
            serviceAccountId, trackedKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);

        try
        {
            // Act — a blank key and an over-budget key are both reported as "nothing to revoke" rather than
            // raising.
            var blankRevoked = await manager.RevokeSessionAsync("   ", ct);
            var overBudgetRevoked = await manager.RevokeSessionAsync(CreateUnreservedKey(MaxSessionKeyBytes + 1), ct);

            // Assert
            blankRevoked.ShouldBeFalse();
            overBudgetRevoked.ShouldBeFalse();

            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, ct))
                .ShouldBe(sharedSlotSentinel, "a blank key must not evict the shared cache slot");
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, trackedKey, ct))
                .ShouldBe(serviceAccountId.ToString("D", CultureInfo.InvariantCulture), "the tracked session's marker must survive");

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == trackedKey, ct)).ShouldBeTrue();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, ct);
        }
    }

    [Fact]
    public async Task RevokeSessionAsync_WhenAMarkerIsWrittenAfterThePreDeleteSweep_RemovesItOnTheSecondSweepAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a tracked session, revoked through a cache that stands in for an issuance landing in the
        // window the second sweep exists for. The real interleaving is a TrackSessionAsync that commits its row
        // and writes its marker after the pre-delete sweep has already run; driving that with two live callers
        // would be timing-dependent, so the stand-in puts the marker back itself on the first removal. The state
        // reached is the one that matters either way: the marker is present again when the row is deleted, and
        // only a sweep after the delete can take it back out.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();

        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            await manager.TrackSessionAsync(
                serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);
        }

        var interleavingCache = CreateCacheRestoringTheMarkerOnTheFirstSweep(cache, serviceAccountId, sessionKey);
        var racedManager = new ServiceAccountSessionManager<TestDbContext>(
            this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(),
            interleavingCache.Object,
            this.App.Services.GetRequiredService<TimeProvider>());

        try
        {
            // Act
            var revoked = await racedManager.RevokeSessionAsync(sessionKey, ct);

            // Assert — with only the pre-delete sweep the restored marker would outlive the row and keep
            // validating a revoked session on the fast path, unreachable by any later revoke because revocation
            // enumerates rows.
            revoked.ShouldBeTrue();
            interleavingCache.Verify(
                c => c.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
                .ShouldBeNull("the sweep after the delete must clear a marker written since the first one");

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
        }
        finally
        {
            // The stand-in writes into the host's cache, which lives in memory and outlives the per-test
            // database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task RevokeServiceAccountSessionsAsync_RemovesEveryRowAndMarkerForTheAccountAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — two live sessions on the account about to be revoked, and one on a second account. This is
        // the account-wide revoke that secret rotation, deactivation and deletion all go through, so it has to
        // clear every session the account owns and nothing beyond it.
        var serviceAccountId = Guid.NewGuid();
        var otherServiceAccountId = Guid.NewGuid();
        var firstKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var secondKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var otherAccountKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();

        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            await manager.TrackSessionAsync(serviceAccountId, firstKey, expiresAt, "test-agent", "127.0.0.1", ct);
            await manager.TrackSessionAsync(serviceAccountId, secondKey, expiresAt, "test-agent", "127.0.0.2", ct);
            await manager.TrackSessionAsync(otherServiceAccountId, otherAccountKey, expiresAt, "test-agent", "127.0.0.3", ct);
        }

        try
        {
            // Act
            int revoked;
            using (var serviceScope = this.App.Services.CreateScope())
            {
                var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
                revoked = await manager.RevokeServiceAccountSessionsAsync(serviceAccountId, ct);
            }

            // Assert — both halves of every revoked session are gone: the marker closes the validation fast
            // path, the row closes the database fallback in ResolveServiceAccountIdAsync. A marker left behind
            // would keep authorizing its token until it expired, and no later revoke could reach it, because
            // revocation enumerates rows.
            revoked.ShouldBe(2);

            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, firstKey, ct)).ShouldBeNull();
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, secondKey, ct)).ShouldBeNull();

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.ServiceAccountId == serviceAccountId, ct))
                .ShouldBeFalse();

            // The second account keeps both halves, so the sweep is scoped by account rather than emptying the
            // table — and its surviving marker shows the markers are removed by key, not flushed wholesale.
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, otherAccountKey, ct))
                .ShouldBe(otherServiceAccountId.ToString("D", CultureInfo.InvariantCulture));
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == otherAccountKey, ct))
                .ShouldBeTrue();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore, and this is
            // the one marker the test deliberately leaves standing.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, otherAccountKey, ct);
        }
    }

    [Fact]
    public async Task RevokeServiceAccountSessionsAsync_WhenAMarkerIsWrittenAfterThePreDeleteSweep_RemovesItOnTheSecondSweepAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — two sessions on one account. The stand-in restores the first key's marker after its
        // pre-delete sweep, which is the interleaving for a session ALREADY IN THE SNAPSHOT: its row is in the
        // delete set, so the marker written over it is one that must not survive the call. A session issued
        // after the snapshot is a different case the snapshot itself handles, by keeping both its row and its
        // marker. The second key runs the ordinary path and shows the stand-in changes nothing for it.
        var serviceAccountId = Guid.NewGuid();
        var racedKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var otherKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();

        using (var serviceScope = this.App.Services.CreateScope())
        {
            var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();
            foreach (var sessionKey in new[] { racedKey, otherKey })
            {
                await manager.TrackSessionAsync(
                    serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct);
            }
        }

        var interleavingCache = CreateCacheRestoringTheMarkerOnTheFirstSweep(cache, serviceAccountId, racedKey);
        var racedManager = new ServiceAccountSessionManager<TestDbContext>(
            this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(),
            interleavingCache.Object,
            this.App.Services.GetRequiredService<TimeProvider>());

        try
        {
            // Act
            var revoked = await racedManager.RevokeServiceAccountSessionsAsync(serviceAccountId, ct);

            // Assert — the snapshot keeps the deleted rows and the swept markers in step, but it does not order
            // this method against an issuance already in flight; the sweep after the delete is what does.
            revoked.ShouldBe(2);
            interleavingCache.Verify(
                c => c.RemoveAsync(CacheId.ServiceAccountSession, racedKey, It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, racedKey, ct))
                .ShouldBeNull("the sweep after the delete must clear a marker written since the first one");
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, otherKey, ct)).ShouldBeNull();

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.ServiceAccountId == serviceAccountId, ct))
                .ShouldBeFalse();
        }
        finally
        {
            // The stand-in writes into the host's cache, which lives in memory and outlives the per-test
            // database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, racedKey, ct);
            await cache.RemoveAsync(CacheId.ServiceAccountSession, otherKey, ct);
        }
    }

    [Fact]
    public async Task TrackSessionAsync_WhenTheCacheRefusesTheMarker_RemovesTheRowItCommittedAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the budget TrackSessionAsync enforces is measured against the cache's default key options,
        // so an application that configures an environment prefix or a schema version has a lower effective
        // ceiling and can still have this write refused for a key that passed the guard. The row is committed
        // before the marker write, and a row the cache cannot address is worse than no session at all: it never
        // validates, and it aborts RevokeServiceAccountSessionsAsync for every other session on the account,
        // which feeds each snapshotted key back to the cache. A cache that refuses the write stands in for that
        // configuration, since a key this host's cache would refuse cannot get past the guard.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var refusingCache = new Mock<IDistributedCache<CacheId>>();
        refusingCache
            .Setup(c => c.SetWithOptionsAsync(
                CacheId.ServiceAccountSession,
                sessionKey,
                It.IsAny<string>(),
                It.IsAny<CacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new ArgumentException("The generated cache key exceeds the maximum length")));

        var dbContextFactory = this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(
            dbContextFactory, refusingCache.Object, this.App.Services.GetRequiredService<TimeProvider>());

        // Act — issuance surfaces the cache failure to the caller...
        await Should.ThrowAsync<ArgumentException>(async () =>
            await manager.TrackSessionAsync(
                serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddHours(1), "test-agent", "127.0.0.1", ct));

        // Assert — ...having written nothing.
        using var dbScope = this.App.CreateDbContextScope();
        (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct))
            .ShouldBeFalse("a session whose marker could not be written must not leave a tracking row behind");
    }

    [Fact]
    public async Task TrackSessionAsync_WhenTheRowDoesNotSurviveIssuance_LeavesNoCacheMarkerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — an already-expired session. Issuance opportunistically reaps this account's expired rows, and
        // the row just inserted is one of them, so issuance reaches its marker write with no row behind it. That
        // is the same end state a revocation racing issuance produces: the row is gone by the time the marker is
        // written, which is the interleaving the post-write check exists to catch.
        var serviceAccountId = Guid.NewGuid();
        var sessionKey = CreateUnreservedKey(MaxSessionKeyBytes);
        var cache = this.App.Services.GetRequiredService<IDistributedCache<CacheId>>();

        using var serviceScope = this.App.Services.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IServiceAccountSessionManager>();

        try
        {
            // Act
            await manager.TrackSessionAsync(
                serviceAccountId, sessionKey, DateTimeOffset.UtcNow.AddMinutes(-1), "test-agent", "127.0.0.1", ct);

            // Assert — a marker outliving its row validates on the fast path and is unreachable by revocation,
            // which enumerates rows. Issuance has to take its own marker back down.
            (await cache.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, ct))
                .ShouldBeNull("a marker must never outlive the tracking row it describes");

            using var dbScope = this.App.CreateDbContextScope();
            (await dbScope.Context.ServiceAccountSessions.AnyAsync(s => s.SessionKey == sessionKey, ct)).ShouldBeFalse();
        }
        finally
        {
            // Cache entries live in the host's memory and outlive the per-test database restore.
            await cache.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, ct);
        }
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_DeletesUpToAndIncludingTheInjectedClockInstantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — three tracking rows straddling the clock instant by a second either side. They are written
        // straight through a DbContext rather than through TrackSessionAsync because issuance reaps this
        // account's rows whose ExpiresAt is at or before its own clock read, on the same write: the expired and
        // boundary rows would delete themselves on the way in. Every expiry sits in the same far-past epoch, so
        // the table-wide delete below cannot reach the fixture's own rows and the count is exactly this test's
        // doing.
        var serviceAccountId = Guid.NewGuid();
        var expiredKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var boundaryKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var liveKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        using (var seedScope = this.App.CreateDbContextScope())
        {
            foreach (var (sessionKey, expiresAt) in new[]
            {
                (expiredKey, ClockEpoch.AddSeconds(-1)),
                (boundaryKey, ClockEpoch),
                (liveKey, ClockEpoch.AddSeconds(1)),
            })
            {
                // Id is left unset so the NEWSEQUENTIALID() store default applies, as it does on the manager's
                // own insert.
                seedScope.Context.ServiceAccountSessions.Add(new BlueprintServiceAccountSession
                {
                    ServiceAccountId = serviceAccountId,
                    SessionKey = sessionKey,
                    CreatedAt = ClockEpoch.AddHours(-1),
                    ExpiresAt = expiresAt,
                    UserAgent = "test-agent",
                    IpAddress = "127.0.0.1",
                });
            }

            await seedScope.Context.SaveChangesAsync(ct);
        }

        // The manager runs on the host's real collaborators but with its own clock, so the time seam can be
        // driven without repointing the shared host's TimeProvider registration for every other test in the
        // collection. The sweep touches only the tracking table, so no cache entry is written here to outlive
        // the per-test database restore.
        var manager = new ServiceAccountSessionManager<TestDbContext>(
            this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(),
            this.App.Services.GetRequiredService<IDistributedCache<CacheId>>(),
            new FakeTimeProvider(ClockEpoch));

        // Act
        var deleted = await manager.CleanupExpiredSessionsAsync(ct);

        // Assert — the filter is ExpiresAt <= now, so the row expiring exactly on the instant goes too, which is
        // what keeps it in step with the opportunistic reap in TrackSessionCoreAsync. Under the wall clock all
        // three would have been swept, the live one included.
        deleted.ShouldBe(2);

        using var dbScope = this.App.CreateDbContextScope();
        var remaining = await dbScope.Context.ServiceAccountSessions
            .AsNoTracking()
            .Where(s => s.ServiceAccountId == serviceAccountId)
            .Select(s => s.SessionKey)
            .ToListAsync(ct);
        remaining.ShouldBe([liveKey]);
    }

    /// <summary>
    /// Wraps the host's cache in a stand-in whose removal of <paramref name="sessionKey"/> performs the real
    /// removal and then, on the first call only, writes the session's marker straight back — the state a
    /// concurrent issuance leaves behind when it writes its marker after revocation's pre-delete sweep has run.
    /// Removals of any other key are passed through unchanged. Revocation calls nothing else on the cache, so
    /// these two setups cover every call it makes and the assertions can read the host's cache directly.
    /// </summary>
    /// <param name="cache">The host's cache, which the stand-in delegates to.</param>
    /// <param name="serviceAccountId">The account the restored marker resolves to.</param>
    /// <param name="sessionKey">The key whose first removal is followed by a marker write.</param>
    /// <returns>The stand-in cache.</returns>
    private static Mock<IDistributedCache<CacheId>> CreateCacheRestoringTheMarkerOnTheFirstSweep(
        IDistributedCache<CacheId> cache, Guid serviceAccountId, string sessionKey)
    {
        var sweeps = 0;
        var standIn = new Mock<IDistributedCache<CacheId>>();

        standIn
            .Setup(c => c.RemoveAsync(CacheId.ServiceAccountSession, sessionKey, It.IsAny<CancellationToken>()))
            .Returns(async (CacheId id, string key, CancellationToken token) =>
            {
                await cache.RemoveAsync(id, key, token);

                if (++sweeps == 1)
                {
                    await cache.SetWithOptionsAsync(
                        id,
                        key,
                        serviceAccountId.ToString("D", CultureInfo.InvariantCulture),
                        new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) },
                        token);
                }
            });

        standIn
            .Setup(c => c.RemoveAsync(
                CacheId.ServiceAccountSession,
                It.Is<string>(key => key != sessionKey),
                It.IsAny<CancellationToken>()))
            .Returns((CacheId id, string key, CancellationToken token) => cache.RemoveAsync(id, key, token));

        return standIn;
    }

    /// <summary>
    /// Builds a session key of the requested character count from URI-unreserved characters only, so it
    /// percent-encodes to itself and one character costs exactly one byte of the cache-key budget. A fresh Guid
    /// leads the key: cache entries live in the host's memory and outlive the per-test database restore, so a
    /// key reused across tests could be resolved by a marker no row backs any more.
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
    /// base64 token can carry that the URI unreserved set does not — '+', '/' and the '=' pad. Each costs three
    /// bytes once percent-encoded, so the key sits on the character budget but over the byte budget.
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
    /// Inserts an active service account in tenant A with the given grants, so a session can be tracked against
    /// an account that really exists and the account's state can then be changed underneath it. The id is
    /// supplied by the caller because the session is tracked against it directly; no seeded service account
    /// exists to borrow.
    /// </summary>
    /// <param name="serviceAccountId">The id to create the account under.</param>
    /// <param name="roles">The account's role assignments.</param>
    /// <param name="permissions">The account's explicit permission grants.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateServiceAccountAsync(Guid serviceAccountId, IList<string> roles, IList<string> permissions)
    {
        var ct = TestContext.Current.CancellationToken;

        using var dbScope = this.App.CreateDbContextScope(SeedData.TenantAId);
        dbScope.Context.ServiceAccounts.Add(new BlueprintServiceAccount
        {
            Id = serviceAccountId,
            TenantId = SeedData.TenantAId,
            Name = "account state probe",
            ClientId = $"account-state-probe-{serviceAccountId:N}",
            ClientSecretHash = "not-a-real-hash",
            IsActive = true,
            Roles = roles,
            Permissions = permissions,
        });

        await dbScope.Context.SaveChangesAsync(ct);
    }
}
