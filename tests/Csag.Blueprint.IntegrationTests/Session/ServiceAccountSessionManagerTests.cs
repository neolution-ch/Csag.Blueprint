namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            (await manager.ValidateSessionAsync(null!, ct)).ShouldBeNull();
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
}
