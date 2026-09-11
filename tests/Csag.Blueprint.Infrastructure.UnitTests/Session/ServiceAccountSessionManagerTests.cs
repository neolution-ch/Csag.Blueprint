namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Globalization;
using System.Text;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Unit tests for the session-key validation in <see cref="ServiceAccountSessionManager{TContext}"/>: the
/// budget is measured in percent-encoded bytes rather than characters, the write path rejects an unusable key
/// at the call site before any I/O, and the read paths report a malformed key as "no session" without ever
/// handing it to the distributed cache. A blank key is refused throughout because the cache drops it and
/// falls back to the shared <see cref="CacheId.ServiceAccountSession"/> slot, which every other blank-keyed
/// call reads and writes.
/// </summary>
public sealed class ServiceAccountSessionManagerTests
{
    // Mirrors SessionKeyMaxCacheKeyBytes in ServiceAccountSessionManager: the distributed cache composes its
    // key as "CacheId:ServiceAccountSession_" + Uri.EscapeDataString(sessionKey) and refuses a generated key
    // over 250 UTF-8 bytes, so the 30-byte prefix leaves 220 bytes for the encoded session key. Every key in
    // this file is derived from this constant so the boundary is stated once.
    private const int MaxSessionKeyBytes = 220;

    // The base64url alphabet: every character is in the URI unreserved set, so percent-encoding leaves it
    // untouched and one character costs exactly one byte of the budget.
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // The standard base64 alphabet, whose last two characters ('+' and '/') are outside the URI unreserved
    // set and therefore cost three bytes each once encoded.
    private const string StandardBase64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private static readonly DateTimeOffset SessionExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

    [Fact]
    public void TrackSessionAsync_WithNullSessionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);

        // Act
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), null!, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyCacheUntouched(cache);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TrackSessionAsync_WithBlankSessionKey_ThrowsArgumentException(string sessionKey)
    {
        // Arrange
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert — Should.Throw matches the type exactly, so this also pins that a blank key produces a plain
        // ArgumentException rather than the ArgumentNullException the null case produces.
        exception.ParamName.ShouldBe("sessionKey");
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public void TrackSessionAsync_WithSessionKeyOneByteOverTheBudget_ThrowsArgumentException()
    {
        // Arrange — one URI-unreserved character past the budget, the smallest possible overshoot.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes + 1);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public async Task TrackSessionAsync_WithBase64UrlSessionKeyAtTheBudget_PassesTheGuard()
    {
        // Arrange — 220 URI-unreserved characters encode to exactly 220 bytes, the longest key the cache
        // accepts. Rejecting it would refuse legitimate keys of the size the token issuer actually mints.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes);

        // Act — the call handing back a task instead of throwing is the first half of the result; where that
        // task then fails is the second half, and the probe factory reports it.
        var track = manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public async Task TrackSessionAsync_WithShortStandardBase64SessionKey_PassesTheGuard()
    {
        // Arrange — a standard-base64 key well inside the budget. Percent-encoding triples '+', '/' and '=',
        // but three times a small number is still small: the guard measures encoded size, so a key must not be
        // refused merely for carrying characters outside the URI unreserved set.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = CreateStandardBase64Key(70);
        EncodedByteCount(sessionKey).ShouldBeLessThanOrEqualTo(MaxSessionKeyBytes);

        // Act
        var track = manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public void TrackSessionAsync_WithNonAsciiSessionKeyInsideTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — 100 characters, each of which percent-encodes to six bytes. This is the other direction a
        // character count diverges from the cache's own measurement, and the larger of the two.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = new string('\u00e4', 100);
        sessionKey.Length.ShouldBeLessThan(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public void TrackSessionAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — the same character count as the accepted base64url key, but drawn from the standard base64
        // alphabet and closed with the '=' pad: '+', '/' and '=' are outside the URI unreserved set and cost
        // three bytes each once encoded, so the generated cache key is over the limit while the character count
        // sits exactly on it. A guard counting characters would admit this key, and since the marker is written
        // only after the row is committed, the cache's refusal would strand a row that can never be validated
        // or revoked — and a row stranded that way also aborts revocation for every other session on the
        // account.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        sessionKey.Length.ShouldBe(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public void TrackSessionAsync_WithRejectedSessionKey_ThrowsAtTheCallSiteRatherThanOnTheReturnedTask()
    {
        // Arrange
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);

        // Act — capture whatever the call returns, so the assertion below can tell a throw at the call site
        // apart from a rejection delivered on a task the caller has to await.
        Task? track = null;
        Should.Throw<ArgumentException>(() =>
        {
            track = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", TestContext.Current.CancellationToken);
        });

        // Assert — validation runs before any asynchronous work starts, so no task was ever handed back. A
        // caller that issues the token first and observes the tracking task later, or not at all, would
        // otherwise hand out a token whose key the cache cannot store and learn of it far from the call.
        track.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateSessionAsync_WithBlankSessionKey_ReturnsNullWithoutTouchingTheCache(string? sessionKey)
    {
        // Arrange — the session-id claim reaching this method is client-supplied.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);

        // Act
        var result = await manager.ValidateSessionAsync(sessionKey!, TestContext.Current.CancellationToken);

        // Assert — a blank key must not reach the cache, which drops it and reads the shared
        // CacheId.ServiceAccountSession slot, resolving the request to whichever account was written there.
        result.ShouldBeNull();
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithSessionKeyOverTheBudget_ReturnsNullWithoutTouchingTheCache()
    {
        // Arrange
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var overByOneByte = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        var atTheCharacterBudget = CreateStandardBase64Key(MaxSessionKeyBytes);
        EncodedByteCount(atTheCharacterBudget).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var overByOneByteResult = await manager.ValidateSessionAsync(overByOneByte, TestContext.Current.CancellationToken);
        var atTheCharacterBudgetResult = await manager.ValidateSessionAsync(atTheCharacterBudget, TestContext.Current.CancellationToken);

        // Assert — no session can have been tracked under either key, so both mean "no session". Passing one
        // on would make the cache throw ArgumentException inside the authentication pipeline, turning a request
        // that should be rejected into an unhandled server error. The second key sits on the character budget,
        // so only a guard measuring encoded bytes keeps it away from the cache.
        overByOneByteResult.ShouldBeNull();
        atTheCharacterBudgetResult.ShouldBeNull();
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public async Task ValidateSessionAsync_WithWellFormedSessionKey_ResolvesTheAccountFromTheCacheMarker()
    {
        // Arrange — a key at the budget, marked live in the cache for an active account. This is the
        // pass-through case that pins the guard to rejecting malformed keys only.
        var serviceAccountId = Guid.NewGuid();
        var factory = new InMemoryDbContextFactory();
        factory.Seed(new BlueprintServiceAccount
        {
            Id = serviceAccountId,
            TenantId = TestDbContextFactory.TestTenantId,
            Name = "Reporting Robot",
            ClientId = "reporting-robot",
            IsActive = true,
            Roles = ["tenant-manager"],
            Permissions = ["tenants.manage"],
        });

        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes);
        var cache = new Mock<IDistributedCache<CacheId>>();
        cache
            .Setup(c => c.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceAccountId.ToString("D", CultureInfo.InvariantCulture));
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);

        // Act
        var result = await manager.ValidateSessionAsync(sessionKey, TestContext.Current.CancellationToken);

        // Assert — the key reached the cache verbatim, and the authorization comes from the account row.
        cache.Verify(
            c => c.GetAsync<string>(CacheId.ServiceAccountSession, sessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
        result.ShouldNotBeNull();
        result.ServiceAccountId.ShouldBe(serviceAccountId);
        result.TenantId.ShouldBe(TestDbContextFactory.TestTenantId);
        result.Roles.ShouldHaveSingleItem().ShouldBe("tenant-manager");
        result.Permissions.ShouldHaveSingleItem().ShouldBe("tenants.manage");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeSessionAsync_WithBlankSessionKey_ReturnsFalseWithoutRemovingTheSharedCacheSlot(string? sessionKey)
    {
        // Arrange
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);

        // Act
        var revoked = await manager.RevokeSessionAsync(sessionKey!, TestContext.Current.CancellationToken);

        // Assert — the cache drops a blank key, so a removal made with one deletes the shared
        // CacheId.ServiceAccountSession entry instead: a caller holding no session at all could evict the
        // marker another session is validating against.
        revoked.ShouldBeFalse();
        VerifyCacheUntouched(cache);
    }

    [Fact]
    public async Task RevokeSessionAsync_WithSessionKeyOverTheBudget_ReturnsFalseWithoutTouchingTheCache()
    {
        // Arrange — one key over by a single byte, and one at the character budget but over the byte budget,
        // the shape most easily mistaken for a usable key.
        var factory = new GuardProbeDbContextFactory();
        var cache = new Mock<IDistributedCache<CacheId>>();
        var manager = new ServiceAccountSessionManager<TestDbContext>(factory, cache.Object);
        var overByOneByte = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        var atTheCharacterBudget = CreateStandardBase64Key(MaxSessionKeyBytes);

        // Act
        var overByOneByteRevoked = await manager.RevokeSessionAsync(overByOneByte, TestContext.Current.CancellationToken);
        var atTheCharacterBudgetRevoked = await manager.RevokeSessionAsync(atTheCharacterBudget, TestContext.Current.CancellationToken);

        // Assert — no such session can exist, and the cache would throw on either key rather than report a
        // miss.
        overByOneByteRevoked.ShouldBeFalse();
        atTheCharacterBudgetRevoked.ShouldBeFalse();
        VerifyCacheUntouched(cache);
    }

    /// <summary>
    /// Builds a session key of the requested character count by cycling through the given alphabet.
    /// </summary>
    /// <param name="alphabet">The characters to draw from, used in order and repeated as needed.</param>
    /// <param name="length">The number of characters in the resulting key.</param>
    /// <returns>The generated session key.</returns>
    private static string CreateKey(string alphabet, int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[i % alphabet.Length]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds a session key of the requested character count in the standard base64 alphabet, closed with the
    /// '=' padding character so the key carries all three characters a base64 token can hold that the URI
    /// unreserved set does not: '+', '/' and '='.
    /// </summary>
    /// <param name="length">The number of characters in the resulting key.</param>
    /// <returns>The generated session key.</returns>
    private static string CreateStandardBase64Key(int length)
        => CreateKey(StandardBase64Alphabet, length - 1) + "=";

    /// <summary>
    /// Counts a key the way the distributed cache counts it: percent-encoded first, then measured in UTF-8
    /// bytes.
    /// </summary>
    /// <param name="sessionKey">The key to measure.</param>
    /// <returns>The size of the encoded key in bytes.</returns>
    private static int EncodedByteCount(string sessionKey)
        => Encoding.UTF8.GetByteCount(Uri.EscapeDataString(sessionKey));

    /// <summary>
    /// Asserts that no cache operation was performed with any key. A rejected session key must never reach the
    /// cache: an over-long one makes it throw, and a blank one is silently redirected to the
    /// <see cref="CacheId.ServiceAccountSession"/> slot every other blank-keyed call shares.
    /// </summary>
    /// <typeparam name="TCache">The mocked cache interface, inferred at the call site.</typeparam>
    /// <param name="cache">The cache mock to inspect.</param>
    private static void VerifyCacheUntouched<TCache>(Mock<TCache> cache)
        where TCache : class, IDistributedCache<CacheId>
    {
        cache.Verify(
            c => c.GetAsync<string>(It.IsAny<CacheId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        cache.Verify(
            c => c.SetWithOptionsAsync(It.IsAny<CacheId>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        cache.Verify(
            c => c.RemoveAsync(It.IsAny<CacheId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // The no-key overloads address the shared slot directly, which is exactly where a dropped blank key
        // would land.
        cache.Verify(
            c => c.GetAsync<string>(It.IsAny<CacheId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        cache.Verify(
            c => c.RemoveAsync(It.IsAny<CacheId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> that hands out no context and throws a recognizable marker
    /// instead. Reaching it is the observable proof that a call cleared the session-key guards, and a test
    /// asserting a rejection fails loudly if the guards let a call through to the database. No database is
    /// involved either way, which also keeps these tests clear of the EF in-memory provider, whose missing
    /// ExecuteDelete support the tracking work would otherwise run into.
    /// </summary>
    internal sealed class GuardProbeDbContextFactory : IDbContextFactory<TestDbContext>
    {
        /// <summary>
        /// The message carried by the marker exception, so a test can tell it apart from an incidental failure.
        /// </summary>
        public const string ReachedMessage = "the session-key guards were cleared";

        /// <inheritdoc/>
        public TestDbContext CreateDbContext() => throw new InvalidOperationException(ReachedMessage);
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> over a single named in-memory database, for the read path
    /// behind an accepted session key.
    /// </summary>
    internal sealed class InMemoryDbContextFactory : IDbContextFactory<TestDbContext>
    {
        private readonly DbContextOptions<TestDbContext> options;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDbContextFactory"/> class.
        /// </summary>
        public InMemoryDbContextFactory()
        {
            // The ambient tenant must be set so the model's tenant query filters can evaluate.
            TenantContext.SetTenant(TestDbContextFactory.TestTenantId);
            this.options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var context = new TestDbContext(this.options);
            context.Database.EnsureCreated();
        }

        /// <inheritdoc/>
        public TestDbContext CreateDbContext() => new(this.options);

        /// <summary>
        /// Inserts service accounts into the backing database.
        /// </summary>
        /// <param name="serviceAccounts">The rows to insert.</param>
        public void Seed(params BlueprintServiceAccount[] serviceAccounts)
        {
            using var context = new TestDbContext(this.options);
            context.Set<BlueprintServiceAccount>().AddRange(serviceAccounts);
            context.SaveChanges();
        }
    }
}
