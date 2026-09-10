namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Globalization;
using System.Text;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// Unit tests for the session-key validation and the tracking-row clamps in
/// <see cref="SessionManager{TUser, TContext}"/>: the budget is measured in percent-encoded bytes rather than
/// characters, the write path rejects an unusable key at the call site before any I/O, and the revoke/untrack
/// paths report a malformed key as "no session" without ever handing it to the ticket cache. A blank key is
/// refused throughout because the cache drops it and falls back to the shared <see cref="CacheId.AuthTicket"/>
/// slot, which every other blank-keyed call reads and writes.
/// </summary>
public sealed class SessionManagerTests
{
    // Mirrors SessionKeyMaxCacheKeyBytes in SessionManager: the distributed cache composes the ticket's key as
    // "CacheId:AuthTicket_" + Uri.EscapeDataString(sessionKey) and refuses a generated key over 250 UTF-8
    // bytes, so the 19-byte prefix leaves 231 bytes for the encoded session key. Every key in this file is
    // derived from this constant so the boundary is stated once.
    private const int MaxSessionKeyBytes = 231;

    // The base64url alphabet: every character is in the URI unreserved set, so percent-encoding leaves it
    // untouched and one character costs exactly one byte of the budget.
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // The standard base64 alphabet, whose last two characters ('+' and '/') are outside the URI unreserved
    // set and therefore cost three bytes each once encoded.
    private const string StandardBase64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    // Mirror the column lengths BlueprintActiveSessionConfiguration maps, which are what the manager clamps to.
    private const int UserAgentMaxLength = 500;

    private static readonly DateTimeOffset SessionExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

    [Fact]
    public void TrackSessionAsync_WithNullSessionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);

        // Act
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), null!, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TrackSessionAsync_WithBlankSessionKey_ThrowsArgumentException(string sessionKey)
    {
        // Arrange
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert — a blank key would take the single row the unique SessionKey index allows for it, while every
        // cache call made with that key lands on the shared CacheId.AuthTicket slot. Should.Throw matches the
        // type exactly, so this also pins that a blank key produces a plain ArgumentException rather than the
        // ArgumentNullException the null case produces.
        exception.ParamName.ShouldBe("sessionKey");
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Fact]
    public void TrackSessionAsync_WithSessionKeyOneByteOverTheBudget_ThrowsArgumentException()
    {
        // Arrange — one URI-unreserved character past the budget, the smallest possible overshoot.
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes + 1);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Fact]
    public void TrackSessionAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — a key whose character count sits exactly on the budget but which carries the base64
        // characters outside the URI unreserved set ('+', '/' and the '=' pad), each costing three bytes once
        // encoded. A guard counting characters would admit it, and the row would then be committed under a key
        // no cache call can compose: the ticket could never be read, rewritten or removed, and the row would
        // abort revocation and refresh for the user's other sessions once the loop reached it.
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        sessionKey.Length.ShouldBe(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Fact]
    public async Task TrackSessionAsync_WithShortStandardBase64SessionKey_PassesTheGuard()
    {
        // Arrange — a standard-base64 key well inside the budget. Percent-encoding triples '+', '/' and '=',
        // but three times a small number is still small: the guard measures encoded size, so a key must not be
        // refused merely for carrying characters outside the URI unreserved set.
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());
        var sessionKey = CreateStandardBase64Key(70);
        EncodedByteCount(sessionKey).ShouldBeLessThanOrEqualTo(MaxSessionKeyBytes);

        // Act
        var track = manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public void TrackSessionAsync_WithNonAsciiSessionKeyInsideTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — 100 characters, each of which percent-encodes to six bytes. This is the other direction a
        // character count diverges from the cache's own measurement, and the larger of the two.
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);
        var sessionKey = new string('\u00e4', 100);
        sessionKey.Length.ShouldBeLessThan(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe("sessionKey");
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Fact]
    public async Task TrackSessionAsync_WithBase64UrlSessionKeyAtTheBudget_PassesTheGuard()
    {
        // Arrange — 231 URI-unreserved characters encode to exactly 231 bytes, the longest key the cache
        // accepts under CacheId.AuthTicket.
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes);

        // Act — the call handing back a task instead of throwing is the first half of the result; where that
        // task then fails is the second half, and the probe factory reports it.
        var track = manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public void TrackSessionAsync_WithRejectedSessionKey_ThrowsAtTheCallSiteRatherThanOnTheReturnedTask()
    {
        // Arrange
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);

        // Act — capture whatever the call returns, so the assertion below can tell a throw at the call site
        // apart from a rejection delivered on a task the caller has to await.
        Task? track = null;
        Should.Throw<ArgumentException>(() =>
        {
            track = manager.TrackSessionAsync(
                Guid.NewGuid(), sessionKey, SessionExpiresAt, "test-agent", "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);
        });

        // Assert — validation runs before any asynchronous work starts, so no task was ever handed back. The
        // caller is a cookie sign-in event that has already cached the ticket and written the cookie, so a
        // rejection observed late, or not at all, would leave a live session with no tracking row.
        track.ShouldBeNull();
    }

    [Fact]
    public async Task TrackSessionAsync_WithOverLongUserAgentAndIpAddress_ClampsThemToTheColumnLengths()
    {
        // Arrange — the User-Agent value is a client-supplied request header bounded only by the server's
        // header limits, so an over-length one must not be able to fail the insert on a field that is only ever
        // displayed back in a session listing.
        var factory = new InMemoryDbContextFactory();
        var manager = CreateManager(factory, new Mock<ITicketCacheService>());
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        // Act
        await manager.TrackSessionAsync(
            Guid.NewGuid(),
            sessionKey,
            SessionExpiresAt,
            new string('a', 900),
            new string('1', 200),
            currentTenantId: null,
            TestContext.Current.CancellationToken);

        // Assert — both values are stored clamped to the lengths BlueprintActiveSessionConfiguration maps.
        await using var dbContext = factory.CreateDbContext();
        var row = await dbContext.Set<BlueprintActiveSession>()
            .AsNoTracking()
            .SingleAsync(s => s.SessionKey == sessionKey, TestContext.Current.CancellationToken);
        row.UserAgent.ShouldNotBeNull().Length.ShouldBe(UserAgentMaxLength);
        row.IpAddress.ShouldNotBeNull().Length.ShouldBe(50);
    }

    [Fact]
    public async Task TrackSessionAsync_WithUserAgentStraddlingTheClampBoundary_StoresNoLoneSurrogate()
    {
        // Arrange — an emoji positioned so the clamp boundary falls between its two surrogates. Cutting there
        // would store half a character: valid nvarchar, but not valid UTF-16 text, so it fails or is mangled
        // wherever the session listing is serialized back out.
        var factory = new InMemoryDbContextFactory();
        var manager = CreateManager(factory, new Mock<ITicketCacheService>());
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var userAgent = new string('a', UserAgentMaxLength - 1) + "\U0001F600" + new string('b', 400);
        char.IsHighSurrogate(userAgent[UserAgentMaxLength - 1]).ShouldBeTrue();

        // Act
        await manager.TrackSessionAsync(
            Guid.NewGuid(), sessionKey, SessionExpiresAt, userAgent, "127.0.0.1", currentTenantId: null, TestContext.Current.CancellationToken);

        // Assert — the clamp stops short of the pair rather than splitting it.
        await using var dbContext = factory.CreateDbContext();
        var row = await dbContext.Set<BlueprintActiveSession>()
            .AsNoTracking()
            .SingleAsync(s => s.SessionKey == sessionKey, TestContext.Current.CancellationToken);
        var stored = row.UserAgent.ShouldNotBeNull();
        stored.Length.ShouldBe(UserAgentMaxLength - 1);
        stored.ShouldNotContain(c => char.IsSurrogate(c));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RevokeOtherUserSessionsAsync_WithBlankKeepSessionKey_ThrowsArgumentException(string? keepSessionKey)
    {
        // Arrange — no row carries a blank key, so "keep everything except this key" would match every session
        // the user has and sign them out of the device making the request as well.
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = manager.RevokeOtherUserSessionsAsync(Guid.NewGuid(), keepSessionKey!, TestContext.Current.CancellationToken);
        });

        // Assert — revoking every session is available deliberately through RevokeUserSessionsAsync, so it must
        // not also be reachable by accident here.
        exception.ParamName.ShouldBe("keepSessionKey");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeSessionAsync_WithBlankSessionKey_ReturnsFalseWithoutRemovingTheSharedCacheSlot(string? sessionKey)
    {
        // Arrange
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);

        // Act
        var revoked = await manager.RevokeSessionAsync(sessionKey!, TestContext.Current.CancellationToken);

        // Assert — the cache drops a blank key, so a removal made with one deletes the shared
        // CacheId.AuthTicket entry instead: a caller holding no session at all could evict what another
        // session authenticates with.
        revoked.ShouldBeFalse();
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Fact]
    public async Task RevokeSessionAsync_WithSessionKeyOverTheBudget_ReturnsFalseWithoutTouchingTheCache()
    {
        // Arrange — one key over by a single byte, and one at the character budget but over the byte budget,
        // the shape most easily mistaken for a usable key.
        var ticketCache = new Mock<ITicketCacheService>();
        var manager = CreateManager(new GuardProbeDbContextFactory(), ticketCache);
        var overByOneByte = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        var atTheCharacterBudget = CreateStandardBase64Key(MaxSessionKeyBytes);

        // Act
        var overByOneByteRevoked = await manager.RevokeSessionAsync(overByOneByte, TestContext.Current.CancellationToken);
        var atTheCharacterBudgetRevoked = await manager.RevokeSessionAsync(atTheCharacterBudget, TestContext.Current.CancellationToken);

        // Assert — no session can have been tracked under either key, and the cache would throw on them rather
        // than report a miss, turning an administrative revoke call into a server error.
        overByOneByteRevoked.ShouldBeFalse();
        atTheCharacterBudgetRevoked.ShouldBeFalse();
        VerifyTicketCacheUntouched(ticketCache);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UntrackSessionAsync_WithBlankSessionKey_ReturnsFalseWithoutQueryingTheDatabase(string? sessionKey)
    {
        // Arrange — reaching the database is what the probe factory reports, so this also pins that the guard
        // runs before the context is created.
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());

        // Act
        var untracked = await manager.UntrackSessionAsync(sessionKey!, TestContext.Current.CancellationToken);

        // Assert — a key no tracked session can carry is reported the same way by both removal methods.
        untracked.ShouldBeFalse();
    }

    [Fact]
    public async Task UntrackSessionAsync_WithSessionKeyOverTheBudget_ReturnsFalse()
    {
        // Arrange — the same two shapes RevokeSessionAsync refuses, so the pair stays in step.
        var manager = CreateManager(new GuardProbeDbContextFactory(), new Mock<ITicketCacheService>());

        // Act
        var overByOneByte = await manager.UntrackSessionAsync(
            CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1), TestContext.Current.CancellationToken);
        var atTheCharacterBudget = await manager.UntrackSessionAsync(
            CreateStandardBase64Key(MaxSessionKeyBytes), TestContext.Current.CancellationToken);

        // Assert
        overByOneByte.ShouldBeFalse();
        atTheCharacterBudget.ShouldBeFalse();
    }

    /// <summary>
    /// Builds a session manager over the given context factory and ticket cache. The user manager and the
    /// tenant authorization resolver are inert: no path exercised here reaches them.
    /// </summary>
    /// <param name="dbContextFactory">The context factory the manager persists and queries through.</param>
    /// <param name="ticketCache">The mocked ticket cache the assertions inspect.</param>
    /// <returns>The manager under test.</returns>
    private static SessionManager<TestUser, TestDbContext> CreateManager(
        IDbContextFactory<TestDbContext> dbContextFactory,
        Mock<ITicketCacheService> ticketCache)
    {
        var userStore = new Mock<IUserStore<TestUser>>();
        var userManager = new Mock<UserManager<TestUser>>(userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        return new SessionManager<TestUser, TestDbContext>(
            ticketCache.Object,
            userManager.Object,
            dbContextFactory,
            Mock.Of<ITenantAuthorizationResolver>());
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
    /// Asserts that no ticket-cache operation was performed. A rejected session key must never reach the
    /// cache: an over-long one makes it throw, and a blank one is silently redirected to the
    /// <see cref="CacheId.AuthTicket"/> slot every other blank-keyed call shares.
    /// </summary>
    /// <param name="ticketCache">The ticket cache mock to inspect.</param>
    private static void VerifyTicketCacheUntouched(Mock<ITicketCacheService> ticketCache)
    {
        ticketCache.Verify(
            c => c.GetTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ticketCache.Verify(
            c => c.SetTicketAsync(It.IsAny<string>(), It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ticketCache.Verify(
            c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> that hands out no context and throws a recognizable marker
    /// instead. Reaching it is the observable proof that a call cleared the session-key guards, and a test
    /// asserting a rejection fails loudly if the guards let a call through to the database.
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
    /// An <see cref="IDbContextFactory{TContext}"/> over a single named in-memory database, for the write path
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
    }
}
