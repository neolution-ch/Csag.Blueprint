namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Custom ITicketStore implementation that stores authentication tickets in strongly-typed distributed cache (IDistributedCache{CacheId}).
/// This enables stateless authentication with immediate session revocation capability.
/// Stores whatever authentication ticket is provided (roles/permissions should be added before storage).
/// The store owns session tracking: every ticket it writes is mirrored into the tracked session row through
/// <see cref="IActiveSessionTracker"/> — sign-in creates the row (or attributes an existing one to the signing-in
/// account), renewal moves it along with the ticket, and removal deletes it.
/// </summary>
/// <remarks>
/// Every write runs to completion rather than observing the request's cancellation: a ticket written without its
/// row, or a row left describing a ticket that was replaced, is exactly the drift the tracking exists to prevent.
/// </remarks>
public sealed class DistributedCacheTicketStore : ITicketStore
{
    /// <summary>Bytes of the session-key digest kept in log tags — wide enough to correlate entries, far too narrow to brute-force back to a key.</summary>
    private const int SessionTagBytes = 6;

    private readonly ITicketCacheService ticketCacheService;
    private readonly IActiveSessionTracker activeSessionTracker;
    private readonly IOptions<BlueprintSessionOptions> sessionOptions;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DistributedCacheTicketStore> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheTicketStore"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="activeSessionTracker">The tracker that keeps the tracked session row in step with the ticket it describes.</param>
    /// <param name="sessionOptions">The session settings: the lifetime of a ticket that arrives without an expiry, and the client address header.</param>
    /// <param name="timeProvider">The clock a ticket without an expiry is dated from.</param>
    /// <param name="logger">The logger.</param>
    public DistributedCacheTicketStore(
        ITicketCacheService ticketCacheService,
        IActiveSessionTracker activeSessionTracker,
        IOptions<BlueprintSessionOptions> sessionOptions,
        TimeProvider timeProvider,
        ILogger<DistributedCacheTicketStore> logger)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.activeSessionTracker = activeSessionTracker ?? throw new ArgumentNullException(nameof(activeSessionTracker));
        this.sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores an authentication ticket in the distributed cache, tracks the new session, and returns its key.
    /// Expects the ticket to already contain roles and permissions as claims. Without a request, the session is
    /// tracked without a user agent or client address.
    /// </summary>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <returns>A unique session key that identifies this ticket in the cache.</returns>
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return this.StoreCoreAsync(ticket, httpContext: null);
    }

    /// <summary>
    /// Stores an authentication ticket in the distributed cache, tracks the new session with the user agent and
    /// client address of <paramref name="httpContext"/>, and returns its key.
    /// </summary>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <param name="httpContext">The request signing in.</param>
    /// <param name="cancellationToken">Not observed; see the remarks on <see cref="DistributedCacheTicketStore"/>.</param>
    /// <returns>A unique session key that identifies this ticket in the cache.</returns>
    public Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return this.StoreCoreAsync(ticket, httpContext);
    }

    /// <summary>
    /// Retrieves an authentication ticket from the distributed cache.
    /// Returns the cached ticket with roles and permissions as they were at login or at the last claims refresh.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <returns>The authentication ticket, or null if not found or expired.</returns>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        return await this.ticketCacheService.GetTicketAsync(key);
    }

    /// <summary>
    /// Removes an authentication ticket from the distributed cache and deletes its tracking row.
    /// Called by the cookie handler on sign-out, including a sign-out raised while it authenticates a request, and
    /// by its expired-ticket branch in the uncommon case where the cache still returns a ticket whose
    /// <c>ExpiresUtc</c> has passed.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAsync(string key)
    {
        // Untracking belongs here rather than in an OnSigningOut handler: the cookie handler passes the
        // session key straight to this method, whereas CookieSigningOutContext does not carry it and an
        // event could only recover it via HttpContext.AuthenticateAsync — which deadlocks when sign-out is
        // raised from inside the handler's own authentication pass.
        //
        // A session that simply expires is not the path this is written for, and rarely reaches it.
        // TicketCacheService caches the ticket with AbsoluteExpiration set to the ticket's own ExpiresUtc, so
        // entry and ticket die together: RetrieveAsync returns null and ReadCookieTicket fails the pass with
        // "Identity missing in session store" before it reaches the branch that discards an expired ticket.
        // That branch needs a ticket still in the cache whose ExpiresUtc is already past, which takes clock
        // skew between writer and reader, or a retrieve whose round trip straddles the expiry instant —
        // ReadCookieTicket samples the clock only once RetrieveAsync has returned. Either way the untracking
        // below is the same work the sign-out path does, so the branch is handled rather than special-cased.
        // Rows of expired sessions that never reach this method are instead deleted by
        // ExpiredSessionCleanupService, and session listings filter them out by ExpiresAt, so what lingers is
        // storage rather than authorization.
        //
        // Delete the tracking row BEFORE removing the cached ticket, for the reason spelled out in
        // SessionManager.RevokeAsync: a concurrent sliding renewal re-writes its ticket unconditionally and
        // only then renews its row, so with the row already gone that renewal reports "no row" and removes its
        // own ticket (see RenewCoreAsync below). The opposite order leaves a window — ticket removed, row still
        // present — in which a renewal both resurrects the ticket and renews the row successfully, and the
        // untracking below then strips the tracking row off a session whose ticket is still live: invisible to
        // listing, unreachable by revocation.
        try
        {
            await this.activeSessionTracker.UntrackAsync(key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A failed row delete must not fail the sign-out, and must not skip the ticket removal below —
            // the cached ticket is what keeps authorizing requests. The row is left for the expired-session
            // cleanup to delete.
            this.logger.LogWarning(ex, "Failed to remove the tracked session row for session {SessionTag}", SessionTag(key));
        }

        await this.ticketCacheService.RemoveTicketAsync(key, CancellationToken.None);
    }

    /// <summary>
    /// Renews an authentication ticket by updating it in the cache and bringing the tracked session row in line
    /// with it. Used for sliding expiration. Without a request, the store cannot tell a sign-in over a live session
    /// cookie from a sliding renewal, and treats the call as a renewal.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <param name="ticket">The updated authentication ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return this.RenewCoreAsync(key, ticket, httpContext: null);
    }

    /// <summary>
    /// Renews an authentication ticket by updating it in the cache and bringing the tracked session row in line
    /// with it. The cookie handler calls this for a sliding renewal, and for a sign-in on a request that still
    /// carries a live session cookie, which keeps that cookie's key. A sliding renewal on a request whose own session
    /// the session lifecycle rewrote keeps the rewritten ticket and takes only the new lifetime from
    /// <paramref name="ticket"/>.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <param name="ticket">The updated authentication ticket.</param>
    /// <param name="httpContext">The request renewing or signing in.</param>
    /// <param name="cancellationToken">Not observed; see the remarks on <see cref="DistributedCacheTicketStore"/>.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return this.RenewCoreAsync(key, ticket, httpContext);
    }

    /// <summary>
    /// Derives a short, non-reversible tag for a session key so that log entries about the same session can
    /// be correlated. The session key is the bearer credential carried by the authentication cookie, so
    /// writing it to a log would let anyone with log access replay the session it belongs to.
    /// </summary>
    /// <param name="key">The session key to describe.</param>
    /// <returns>A hex tag derived from the key, safe to record in logs.</returns>
    private static string SessionTag(string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(digest.AsSpan(0, SessionTagBytes));
    }

    private static Guid ReadAccountId(AuthenticationTicket ticket)
    {
        // A session that cannot be attributed to an account has no row any revocation could find, so the store
        // refuses it rather than hand out a cookie nobody can end.
        return SessionPrincipal.TryGetAccountId(ticket.Principal, out var accountId)
            ? accountId
            : throw new InvalidOperationException(
                "The authentication ticket carries no account identifier (a Guid NameIdentifier claim), so its session could not be tracked.");
    }

    private async Task<string> StoreCoreAsync(AuthenticationTicket ticket, HttpContext? httpContext)
    {
        var accountId = ReadAccountId(ticket);
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var expiresUtc = this.EnsureExpiry(ticket);

        // Store session key in ticket properties for access in cookie authentication events
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName] = sessionKey;

        // Ticket first, then the row — the order RenewCoreAsync follows too. The cookie that carries the key is
        // only written once this method returns, so nothing can use the ticket before its row exists, while a
        // revocation of the account that runs in between finds the row and ends the session.
        await this.ticketCacheService.SetTicketAsync(sessionKey, ticket, expiresUtc, CancellationToken.None);
        await this.TrackOrRemoveTicketAsync(this.CreateEntry(sessionKey, accountId, ticket, expiresUtc, httpContext));

        return sessionKey;
    }

    private async Task RenewCoreAsync(string key, AuthenticationTicket ticket, HttpContext? httpContext)
    {
        var signingIn = SessionCookieEvents.IsSigningIn(httpContext);
        if (!signingIn && httpContext.WasSessionRewritten(key))
        {
            // The cookie handler captured the ticket it renews when it authenticated the request, before the session
            // lifecycle rewrote this session's claims or tenant later in the same request. Writing that ticket would
            // undo the rewrite, so the renewal carries the cached principal forward with the handler's new lifetime.
            var current = await this.ticketCacheService.GetTicketAsync(key, CancellationToken.None);
            if (current is null)
            {
                // The session ended during the request; renewing it would bring it back.
                return;
            }

            ticket = new AuthenticationTicket(current.Principal, ticket.Properties, ticket.AuthenticationScheme);
        }

        var accountId = ReadAccountId(ticket);
        var expiresUtc = this.EnsureExpiry(ticket);

        // A sign-in over a live cookie hands over fresh properties without the key; the cached ticket must carry
        // it, because that is where the session key is read back from on later requests.
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName] = key;

        await this.ticketCacheService.SetTicketAsync(key, ticket, expiresUtc, CancellationToken.None);

        var entry = this.CreateEntry(key, accountId, ticket, expiresUtc, httpContext);

        if (signingIn)
        {
            // A sign-in reuses the key of the session cookie the request still carries, possibly for a different
            // account. The row is attributed to the signing-in account — or created, when a revocation removed it
            // in the meantime — and a sign-in that cannot record its session fails rather than leave the row
            // naming the previous account.
            await this.TrackOrRemoveTicketAsync(entry);
            return;
        }

        // Keep the tracked session row in step with the renewed ticket. Otherwise a long-lived sliding session
        // outlives its row and disappears from session listing, revocation, and refresh — so an authorization
        // change (e.g. a demotion) would never reach a user who simply stays active.
        bool sessionStillTracked;
        try
        {
            sessionStillTracked = await this.activeSessionTracker.RenewAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A failed row update must not fail the renewal itself: the cache ticket is already
            // renewed, and the row merely lags until the next successful renewal. Only a definitive
            // "no row" answer (below) is treated as revoked.
            this.logger.LogWarning(ex, "Failed to renew the tracked session row for session {SessionTag}", SessionTag(key));
            return;
        }

        // No tracking row means the session was revoked (or its expired row was cleaned up) while this renewal
        // was in flight. The SetTicketAsync above has just resurrected the ticket of a session that no longer
        // exists administratively: it would be invisible to session listing, unreachable by revocation and
        // refresh, and would keep sliding forever. Fail closed by removing the ticket again — at worst a
        // legitimately active user has to sign in again.
        if (!sessionStillTracked)
        {
            await this.ticketCacheService.RemoveTicketAsync(key, CancellationToken.None);
            this.logger.LogWarning("Session {SessionTag} was renewed concurrently with its revocation; the renewed ticket has been removed.", SessionTag(key));
        }
    }

    private async Task TrackOrRemoveTicketAsync(SessionTrackingEntry entry)
    {
        try
        {
            await this.activeSessionTracker.TrackAsync(entry, CancellationToken.None);
        }
        catch (Exception)
        {
            // The sign-in fails with the tracking error. The ticket goes too, so no cookie can ever be issued for a
            // session no revocation could find.
            await this.ticketCacheService.RemoveTicketAsync(entry.SessionKey, CancellationToken.None);
            throw;
        }
    }

    private DateTimeOffset EnsureExpiry(AuthenticationTicket ticket)
    {
        // The cookie handler stamps ExpiresUtc from ExpireTimeSpan — the configured lifetime — before calling the
        // store, unless the sign-in set its own. A ticket that reaches the store another way gets the same lifetime,
        // so the cache entry, the row and any cookie built from the ticket expire together.
        return ticket.Properties.ExpiresUtc ??= this.timeProvider.GetUtcNow().Add(this.sessionOptions.Value.Lifetime);
    }

    private SessionTrackingEntry CreateEntry(
        string sessionKey,
        Guid accountId,
        AuthenticationTicket ticket,
        DateTimeOffset expiresUtc,
        HttpContext? httpContext)
    {
        string? userAgent = null;
        string? ipAddress = null;

        if (httpContext is not null)
        {
            var userAgentHeader = httpContext.Request.Headers.UserAgent.ToString();
            userAgent = userAgentHeader.Length == 0 ? null : userAgentHeader;

            if (httpContext.TryGetClientAddress(this.sessionOptions.Value.ClientIpHeader, out var address))
            {
                ipAddress = (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
            }
        }

        return new SessionTrackingEntry(
            sessionKey,
            accountId,
            SessionPrincipal.GetTenantId(ticket.Principal),
            expiresUtc,
            userAgent,
            ipAddress);
    }
}
