namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Net;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

/// <summary>
/// Unit tests for <see cref="DistributedCacheTicketStore"/> tracking behavior: the store records every session it
/// writes, with the signing-in request's details; a sign-in over a live session cookie attributes the existing
/// session to the signing-in account; a renewal must not resurrect a session whose tracking row is gone (revoked
/// concurrently); a transient renewal failure must not kill a legitimate session; and a removal must take the
/// tracking row with it.
/// </summary>
public sealed class DistributedCacheTicketStoreTests
{
    private const string SessionKey = "test-session-key";
    private const string ClientIpHeader = "X-Client-IP";

    private static readonly Guid AccountId = new Guid("2f0d4c1e-8a37-4f5b-9d0e-6c1b7a2e3f40");
    private static readonly Guid TenantId = new Guid("8c3e7a91-1b2d-4e5f-a6b7-c8d9e0f1a2b3");
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    [Fact]
    public async Task StoreAsync_TracksTheSessionWithTheRequestsDetails()
    {
        // Arrange
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(ticketCache, tracker);
        var ticket = CreateTicket(TenantId);
        var httpContext = CreateRequest("203.0.113.7", "10.0.0.1");

        // Act
        var sessionKey = await store.StoreAsync(ticket, httpContext, TestContext.Current.CancellationToken);

        // Assert — the row describes the ticket and the request that signed in; the address is the edge-stamped
        // client address, not the connection's.
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName].ShouldBe(sessionKey);
        tracker.Verify(
            t => t.TrackAsync(
                new SessionTrackingEntry(sessionKey, AccountId, TenantId, ticket.Properties.ExpiresUtc!.Value, "unit-test-agent", "203.0.113.7"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, "198.51.100.4", "198.51.100.4")]
    [InlineData("not-an-address", "198.51.100.4", "198.51.100.4")]
    [InlineData(null, "::ffff:198.51.100.4", "198.51.100.4")]
    public async Task StoreAsync_WithoutAUsableClientAddressHeader_RecordsTheConnectionAddress(
        string? headerValue, string remoteAddress, string expectedAddress)
    {
        // Arrange
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(new Mock<ITicketCacheService>(), tracker);

        // Act
        await store.StoreAsync(CreateTicket(), CreateRequest(headerValue, remoteAddress), TestContext.Current.CancellationToken);

        // Assert
        tracker.Verify(
            t => t.TrackAsync(It.Is<SessionTrackingEntry>(e => e.IpAddress == expectedAddress), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WithoutARequest_TracksTheSessionWithoutRequestDetails()
    {
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(new Mock<ITicketCacheService>(), tracker);

        await store.StoreAsync(CreateTicket());

        tracker.Verify(
            t => t.TrackAsync(It.Is<SessionTrackingEntry>(e => e.UserAgent == null && e.IpAddress == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreAsync_TicketWithoutAnExpiry_ExpiresAfterTheConfiguredLifetime()
    {
        // Arrange — the cookie handler always stamps an expiry; a ticket that reaches the store another way gets the
        // configured lifetime rather than none.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(ticketCache, tracker, clock);
        var ticket = CreateTicket();
        ticket.Properties.ExpiresUtc = null;

        // Act
        var sessionKey = await store.StoreAsync(ticket);

        // Assert
        var expected = clock.GetUtcNow().Add(Lifetime);
        ticket.Properties.ExpiresUtc.ShouldBe(expected);
        ticketCache.Verify(c => c.SetTicketAsync(sessionKey, ticket, expected, It.IsAny<CancellationToken>()), Times.Once);
        tracker.Verify(t => t.TrackAsync(It.Is<SessionTrackingEntry>(e => e.ExpiresAt == expected), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WritesTheTicketBeforeTrackingIt()
    {
        // The cookie carrying the key is only issued once the store returns, so nothing can use the ticket before
        // its row exists, while a revocation that runs between the two writes still finds the row.
        var calls = new List<string>();
        var ticketCache = new Mock<ITicketCacheService>();
        ticketCache
            .Setup(c => c.SetTicketAsync(It.IsAny<string>(), It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("ticket"))
            .Returns(Task.CompletedTask);
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.TrackAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("row"))
            .Returns(Task.CompletedTask);
        var store = CreateStore(ticketCache, tracker);

        await store.StoreAsync(CreateTicket());

        calls.ShouldBe(["ticket", "row"]);
    }

    [Fact]
    public async Task StoreAsync_TrackingFails_RemovesTheTicketAndFailsTheSignIn()
    {
        // A session without a row cannot be listed or revoked, so no cookie may be issued for it.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.TrackAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("transient database failure")));
        var store = CreateStore(ticketCache, tracker);

        await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(CreateTicket()));

        ticketCache.Verify(c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_TicketWithoutAnAccountId_StoresNothing()
    {
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(ticketCache, tracker);
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()), IdentityConstants.ApplicationScheme);

        await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(ticket));

        ticketCache.Verify(
            c => c.SetTicketAsync(It.IsAny<string>(), It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        tracker.Verify(t => t.TrackAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenewAsync_SessionStillTracked_KeepsRenewedTicket()
    {
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.RenewAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var store = CreateStore(ticketCache, tracker);

        await store.RenewAsync(SessionKey, CreateTicket(TenantId));

        ticketCache.Verify(
            c => c.SetTicketAsync(SessionKey, It.IsAny<AuthenticationTicket>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        ticketCache.Verify(
            c => c.RemoveTicketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        tracker.Verify(
            t => t.RenewAsync(It.Is<SessionTrackingEntry>(e => e.SessionKey == SessionKey && e.UserId == AccountId && e.TenantId == TenantId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenewAsync_TrackingRowGone_RemovesRenewedTicket()
    {
        // The tracking row disappears when the session is revoked (or its expired row was cleaned up).
        // Re-writing the ticket would resurrect a session that is invisible to session listing and
        // unreachable by revocation/refresh — the renewal must fail closed and remove it again.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.RenewAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var store = CreateStore(ticketCache, tracker);

        await store.RenewAsync(SessionKey, CreateTicket());

        ticketCache.Verify(
            c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenewAsync_RowUpdateFails_KeepsTicketAndDoesNotThrow()
    {
        // A transient row-update failure is not a revocation signal: the ticket stays renewed and the
        // row merely lags until the next successful renewal. Only a definitive "no row" answer revokes.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.RenewAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
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
    public async Task RenewAsync_DuringASignIn_AttributesTheSessionToTheSigningInAccount()
    {
        // Arrange — a sign-in on a request that still carries a live session cookie reuses that cookie's key, so
        // the cookie handler calls RenewAsync with the new account's ticket. The package's sign-in event marks the
        // request first.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        var store = CreateStore(ticketCache, tracker);
        var httpContext = await SignInThroughTheCookieEventsAsync();
        var ticket = CreateTicket(TenantId);

        // Act
        await store.RenewAsync(SessionKey, ticket, httpContext, TestContext.Current.CancellationToken);

        // Assert — recorded as a sign-in (attribute or create), not renewed; and the cached ticket carries its key.
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName].ShouldBe(SessionKey);
        tracker.Verify(
            t => t.TrackAsync(It.Is<SessionTrackingEntry>(e => e.SessionKey == SessionKey && e.UserId == AccountId && e.TenantId == TenantId), It.IsAny<CancellationToken>()),
            Times.Once);
        tracker.Verify(t => t.RenewAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RenewAsync_DuringASignInWhoseTrackingFails_RemovesTheTicketAndFailsTheSignIn()
    {
        // Leaving the ticket would leave the row naming the previous account while the cookie authenticates the new
        // one: revoking the new account would miss the session.
        var ticketCache = new Mock<ITicketCacheService>();
        var tracker = new Mock<IActiveSessionTracker>();
        tracker
            .Setup(t => t.TrackAsync(It.IsAny<SessionTrackingEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("transient database failure")));
        var store = CreateStore(ticketCache, tracker);
        var httpContext = await SignInThroughTheCookieEventsAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.RenewAsync(SessionKey, CreateTicket(), httpContext, TestContext.Current.CancellationToken));

        ticketCache.Verify(c => c.RemoveTicketAsync(SessionKey, It.IsAny<CancellationToken>()), Times.Once);
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
        // A concurrent sliding renewal re-writes its ticket unconditionally and only then renews its row.
        // Removing the ticket first leaves a window in which such a renewal resurrects the ticket AND
        // renews the still-present row, so the untracking would strip the tracking row off a live session.
        // Deleting the row first makes that renewal report "no row", and the renewal removes its own ticket.
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
        // ticket is what keeps authorizing requests; the stale row is deleted by the expired-session cleanup.
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

    private static DistributedCacheTicketStore CreateStore(
        Mock<ITicketCacheService> ticketCache,
        Mock<IActiveSessionTracker> tracker,
        TimeProvider? clock = null)
    {
        var options = Options.Create(new BlueprintSessionOptions { Lifetime = Lifetime, ClientIpHeader = ClientIpHeader });
        return new DistributedCacheTicketStore(
            ticketCache.Object,
            tracker.Object,
            options,
            clock ?? TimeProvider.System,
            NullLogger<DistributedCacheTicketStore>.Instance);
    }

    private static AuthenticationTicket CreateTicket(Guid? tenantId = null)
    {
        var ticket = new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId, tenantId), IdentityConstants.ApplicationScheme);

        // The cookie handler always assigns ExpiresUtc before calling the store.
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);
        return ticket;
    }

    private static DefaultHttpContext CreateRequest(string? clientIpHeaderValue, string remoteAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "unit-test-agent";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        if (clientIpHeaderValue is not null)
        {
            httpContext.Request.Headers[ClientIpHeader] = clientIpHeaderValue;
        }

        return httpContext;
    }

    private static async Task<HttpContext> SignInThroughTheCookieEventsAsync()
    {
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var eligibility = new Mock<ISessionEligibility>();
        eligibility.Setup(e => e.MayHoldSessionAsync(AccountId)).ReturnsAsync(true);
        var httpContext = SessionTestServices.CreateHttpContext(eligibility.Object);

        await options.Events.SigningIn(new CookieSigningInContext(
            httpContext,
            SessionTestServices.SessionScheme(),
            options,
            SessionTestServices.CreatePrincipal(AccountId),
            new AuthenticationProperties(),
            new CookieOptions()));

        return httpContext;
    }
}
