namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Text;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Unit tests for the session-key validation in <see cref="ActiveSessionTracker{TContext}"/>: the budget is measured
/// in percent-encoded bytes rather than characters, and the write path rejects an unusable key at the call site
/// before any I/O. A blank key is refused because the cache drops it and falls back to the shared
/// <see cref="CacheId.AuthTicket"/> slot, which every other blank-keyed call reads and writes. The write itself, and
/// the clamping of the diagnostic columns, run against SQL Server in the integration suite.
/// </summary>
public sealed class ActiveSessionTrackerTests
{
    // Mirrors SessionConstants.SessionKeyMaxCacheKeyBytes: the distributed cache composes the ticket's key as
    // "CacheId:AuthTicket_" + Uri.EscapeDataString(sessionKey) and refuses a generated key over 250 UTF-8 bytes, so
    // the 19-byte prefix leaves 231 bytes for the encoded session key. Every key in this file is derived from this
    // constant so the boundary is stated once.
    private const int MaxSessionKeyBytes = 231;

    // The base64url alphabet: every character is in the URI unreserved set, so percent-encoding leaves it
    // untouched and one character costs exactly one byte of the budget.
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // The standard base64 alphabet, whose last two characters ('+' and '/') are outside the URI unreserved
    // set and therefore cost three bytes each once encoded.
    private const string StandardBase64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    private const string SessionKeyParameter = "entry.SessionKey";

    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 5, 2, 11, 30, 0, TimeSpan.Zero));

    [Fact]
    public void TrackAsync_WithNullSessionKey_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = CreateTracker();

        // Act
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = tracker.TrackAsync(CreateEntry(null!), TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe(SessionKeyParameter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TrackAsync_WithBlankSessionKey_ThrowsArgumentException(string sessionKey)
    {
        // Arrange
        var tracker = CreateTracker();

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);
        });

        // Assert — a blank key would take the single row the unique SessionKey index allows for it, while every
        // cache call made with that key lands on the shared CacheId.AuthTicket slot. Should.Throw matches the
        // type exactly, so this also pins that a blank key produces a plain ArgumentException rather than the
        // ArgumentNullException the null case produces.
        exception.ParamName.ShouldBe(SessionKeyParameter);
    }

    [Fact]
    public void TrackAsync_WithSessionKeyOneByteOverTheBudget_ThrowsArgumentException()
    {
        // Arrange — one URI-unreserved character past the budget, the smallest possible overshoot.
        var tracker = CreateTracker();
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes + 1);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe(SessionKeyParameter);
    }

    [Fact]
    public void TrackAsync_WithStandardBase64SessionKeyAtTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — a key whose character count sits exactly on the budget but which carries the base64
        // characters outside the URI unreserved set ('+', '/' and the '=' pad), each costing three bytes once
        // encoded. A guard counting characters would admit it, and the row would then be committed under a key
        // no cache call can compose: the ticket could never be read, rewritten or removed, and the row would
        // abort revocation and refresh for the user's other sessions once the loop reached it.
        var tracker = CreateTracker();
        var sessionKey = CreateStandardBase64Key(MaxSessionKeyBytes);
        sessionKey.Length.ShouldBe(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe(SessionKeyParameter);
    }

    [Fact]
    public async Task TrackAsync_WithShortStandardBase64SessionKey_PassesTheGuard()
    {
        // Arrange — a standard-base64 key well inside the budget. Percent-encoding triples '+', '/' and '=',
        // but three times a small number is still small: the guard measures encoded size, so a key must not be
        // refused merely for carrying characters outside the URI unreserved set.
        var tracker = CreateTracker();
        var sessionKey = CreateStandardBase64Key(70);
        EncodedByteCount(sessionKey).ShouldBeLessThanOrEqualTo(MaxSessionKeyBytes);

        // Act
        var track = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public void TrackAsync_WithNonAsciiSessionKeyInsideTheCharacterBudget_ThrowsArgumentException()
    {
        // Arrange — 100 characters, each of which percent-encodes to six bytes. This is the other direction a
        // character count diverges from the cache's own measurement, and the larger of the two.
        var tracker = CreateTracker();
        var sessionKey = new string('ä', 100);
        sessionKey.Length.ShouldBeLessThan(MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBeGreaterThan(MaxSessionKeyBytes);

        // Act
        var exception = Should.Throw<ArgumentException>(() =>
        {
            _ = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);
        });

        // Assert
        exception.ParamName.ShouldBe(SessionKeyParameter);
    }

    [Fact]
    public async Task TrackAsync_WithBase64UrlSessionKeyAtTheBudget_PassesTheGuard()
    {
        // Arrange — 231 URI-unreserved characters encode to exactly 231 bytes, the longest key the cache
        // accepts under CacheId.AuthTicket.
        var tracker = CreateTracker();
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes);
        EncodedByteCount(sessionKey).ShouldBe(MaxSessionKeyBytes);

        // Act — the call handing back a task instead of throwing is the first half of the result; where that
        // task then fails is the second half, and the probe factory reports it.
        var track = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);

        // Assert — the failure comes from the tracking work behind the guard, so the key cleared the guard.
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await track);
        exception.Message.ShouldBe(GuardProbeDbContextFactory.ReachedMessage);
    }

    [Fact]
    public void TrackAsync_WithRejectedSessionKey_ThrowsAtTheCallSiteRatherThanOnTheReturnedTask()
    {
        // Arrange
        var tracker = CreateTracker();
        var sessionKey = CreateKey(Base64UrlAlphabet, MaxSessionKeyBytes + 1);

        // Act — capture whatever the call returns, so the assertion below can tell a throw at the call site
        // apart from a rejection delivered on a task the caller has to await.
        Task? track = null;
        Should.Throw<ArgumentException>(() =>
        {
            track = tracker.TrackAsync(CreateEntry(sessionKey), TestContext.Current.CancellationToken);
        });

        // Assert — validation runs before any asynchronous work starts, so no task was ever handed back.
        track.ShouldBeNull();
    }

    private static ActiveSessionTracker<TestDbContext> CreateTracker() => new(new GuardProbeDbContextFactory(), Clock);

    private static SessionTrackingEntry CreateEntry(string sessionKey)
        => new(sessionKey, Guid.NewGuid(), TenantId: null, Clock.GetUtcNow().AddHours(1), "test-agent", "127.0.0.1");

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
}
