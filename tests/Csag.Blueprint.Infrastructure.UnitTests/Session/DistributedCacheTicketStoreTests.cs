namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="DistributedCacheTicketStore"/> tracking behavior: a renewal must not
/// resurrect a session whose tracking row is gone (revoked concurrently), a transient extension
/// failure must not kill a legitimate session, and a removal must take the tracking row with it.
/// </summary>
public sealed class DistributedCacheTicketStoreTests
{
    private const string SessionKey = "test-session-key";

    [Fact]
    public async Task RenewAsync_SessionStillTracked_KeepsRenewedTicket()
    {
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var store = CreateStore(ticketCache, tracker);

        await store.RenewAsync(SessionKey, CreateTicket());

        ticketCache.Verify(
            c => c.SetTicketAsync(SessionKey, It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        ticketCache.Verify(
            c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RenewAsync_TrackingRowGone_RemovesRenewedTicket()
    {
        // The tracking row disappears when the session is revoked (or its lagging row was cleaned up as
        // expired). Re-writing the ticket would resurrect a session that is invisible to session listing
        // and unreachable by revocation/refresh — the renewal must fail closed and remove it again.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var store = CreateStore(ticketCache, tracker);

        await store.RenewAsync(SessionKey, CreateTicket());

        ticketCache.Verify(
            c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenewAsync_ExtensionFails_KeepsTicketAndDoesNotThrow()
    {
        // A transient row-update failure is not a revocation signal: the ticket stays renewed and the
        // row merely lags until the next successful renewal. Only a definitive "no row" answer revokes.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient database failure"));
        var store = CreateStore(ticketCache, tracker);

        await Should.NotThrowAsync(() => store.RenewAsync(SessionKey, CreateTicket()));

        ticketCache.Verify(
            c => c.SetTicketAsync(SessionKey, It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        ticketCache.Verify(
            c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTicketAndTrackingRow()
    {
        // Untracking lives here rather than in an OnSigningOut handler: the cookie handler hands the session
        // key to RemoveAsync, while CookieSigningOutContext does not carry it. Every sign-out must take the
        // tracking row with it, or a session that ended would keep showing up as active.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.UntrackAsync(SessionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var store = CreateStore(ticketCache, tracker);

        await store.RemoveAsync(SessionKey);

        ticketCache.Verify(
            c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
        tracker.Verify(
            t => t.UntrackAsync(SessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTrackingRowBeforeTicket()
    {
        // A concurrent sliding renewal re-writes its ticket unconditionally and only then extends its row.
        // Removing the ticket first leaves a window in which such a renewal resurrects the ticket AND
        // extends the still-present row, so the untracking would strip the tracking row off a live session.
        // Deleting the row first makes that extension report "no row", and the renewal removes its own ticket.
        var calls = new List<string>();
        var ticketCache = new Mock<ITicketCacheService>();
        ticketCache
            .Setup(c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("ticket"))
            .Returns(Task.CompletedTask);
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.UntrackAsync(SessionKey, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("row"))
            .ReturnsAsync(true);
        var store = CreateStore(ticketCache, tracker);

        await store.RemoveAsync(SessionKey);

        calls.ShouldBe(["row", "ticket"]);
    }

    [Fact]
    public async Task RemoveAsync_UntrackFails_StillRemovesTicketAndDoesNotThrow()
    {
        // A failed row delete must not fail the sign-out, and must not skip the ticket removal — the cached
        // ticket is what keeps authorizing requests; the stale row is reaped by CleanupExpiredSessionsAsync.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.UntrackAsync(SessionKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient database failure"));
        var store = CreateStore(ticketCache, tracker);

        await Should.NotThrowAsync(() => store.RemoveAsync(SessionKey));

        ticketCache.Verify(
            c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static DistributedCacheTicketStore CreateStore(Mock<ITicketCacheService> ticketCache, Mock<IActiveSessionTracker> tracker)
    {
        return new DistributedCacheTicketStore(ticketCache.Object, tracker.Object, NullLogger<DistributedCacheTicketStore>.Instance);
    }

    private static AuthenticationTicket CreateTicket()
    {
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), "TestScheme");

        // RenewAsync requires ExpiresUtc — the cookie handler always assigns it before renewing.
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);
        return ticket;
    }
}
