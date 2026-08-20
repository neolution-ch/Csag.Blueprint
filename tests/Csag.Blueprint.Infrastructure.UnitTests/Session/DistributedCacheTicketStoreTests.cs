namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="DistributedCacheTicketStore"/> sliding-renewal behavior: a renewal must
/// not resurrect a session whose tracking row is gone (revoked concurrently), while a transient
/// extension failure must not kill a legitimate session.
/// </summary>
public sealed class DistributedCacheTicketStoreTests
{
    private const string SessionKey = "test-session-key";

    [Fact]
    public async Task RenewAsync_SessionStillTracked_KeepsRenewedTicket()
    {
        var ticketCache = new Mock<ITicketCacheService>();
        var extender = new Mock<ISessionExpirationExtender>();
        extender
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var store = CreateStore(ticketCache, extender);

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
        var extender = new Mock<ISessionExpirationExtender>();
        extender
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var store = CreateStore(ticketCache, extender);

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
        var extender = new Mock<ISessionExpirationExtender>();
        extender
            .Setup(e => e.ExtendAsync(SessionKey, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient database failure"));
        var store = CreateStore(ticketCache, extender);

        await Should.NotThrowAsync(() => store.RenewAsync(SessionKey, CreateTicket()));

        ticketCache.Verify(
            c => c.SetTicketAsync(SessionKey, It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        ticketCache.Verify(
            c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DistributedCacheTicketStore CreateStore(Mock<ITicketCacheService> ticketCache, Mock<ISessionExpirationExtender> extender)
    {
        return new DistributedCacheTicketStore(ticketCache.Object, extender.Object, NullLogger<DistributedCacheTicketStore>.Instance);
    }

    private static AuthenticationTicket CreateTicket()
    {
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), "TestScheme");

        // RenewAsync requires ExpiresUtc — the cookie handler always assigns it before renewing.
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);
        return ticket;
    }
}
