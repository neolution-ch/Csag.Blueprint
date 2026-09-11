namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom ITicketStore implementation that stores authentication tickets in strongly-typed distributed cache (IDistributedCache{CacheId}).
/// This enables stateless authentication with immediate session revocation capability.
/// Stores whatever authentication ticket is provided (roles/permissions should be added before storage).
/// The tracked session row follows the ticket through <see cref="IActiveSessionTracker"/>: renewal extends it,
/// removal deletes it. Row creation stays in the OnSignedIn cookie event, which is where the sign-in details are assembled.
/// </summary>
public sealed class DistributedCacheTicketStore : ITicketStore
{
    /// <summary>Bytes of the session-key digest kept in log tags — wide enough to correlate entries, far too narrow to brute-force back to a key.</summary>
    private const int SessionTagBytes = 6;

    private readonly ITicketCacheService ticketCacheService;
    private readonly IActiveSessionTracker activeSessionTracker;
    private readonly ILogger<DistributedCacheTicketStore> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheTicketStore"/> class.
    /// </summary>
    /// <param name="ticketCacheService">The ticket cache service for managing authentication tickets.</param>
    /// <param name="activeSessionTracker">The tracker that keeps the tracked session row in step with the ticket it describes.</param>
    /// <param name="logger">The logger.</param>
    public DistributedCacheTicketStore(
        ITicketCacheService ticketCacheService,
        IActiveSessionTracker activeSessionTracker,
        ILogger<DistributedCacheTicketStore> logger)
    {
        this.ticketCacheService = ticketCacheService ?? throw new ArgumentNullException(nameof(ticketCacheService));
        this.activeSessionTracker = activeSessionTracker ?? throw new ArgumentNullException(nameof(activeSessionTracker));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores an authentication ticket in the distributed cache and returns a session key.
    /// Expects the ticket to already contain roles and permissions as claims.
    /// </summary>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <returns>A unique session key that identifies this ticket in the cache.</returns>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var sessionKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        // HandleSignInAsync assigns ExpiresUtc from IssuedUtc + ExpireTimeSpan whenever the sign-in did not
        // supply one, and does so before StoreAsync runs. A null here therefore means the ticket did not come
        // from the cookie handler at all, which is a misconfigured pipeline rather than a state to paper over.
        var expiresUtc = ticket.Properties.ExpiresUtc
            ?? throw new InvalidOperationException(
                "Authentication ticket has no ExpiresUtc when storing the session. "
                + "Tickets must reach the store through CookieAuthenticationHandler, which derives ExpiresUtc from ExpireTimeSpan before StoreAsync runs.");

        // Store session key in ticket properties for access in cookie authentication events
        ticket.Properties.Items[SessionConstants.SessionKeyPropertyName] = sessionKey;

        // Store in cache
        await this.ticketCacheService.SetTicketAsync(sessionKey, ticket, expiresUtc);

        return sessionKey;
    }

    /// <summary>
    /// Retrieves an authentication ticket from the distributed cache.
    /// Returns the cached ticket with roles and permissions as they were at login.
    /// To refresh roles/permissions after changes, use ISessionManager.RefreshUserSessionsAsync().
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <returns>The authentication ticket, or null if not found or expired.</returns>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        return await this.ticketCacheService.GetTicketAsync(key);
    }

    /// <summary>
    /// Removes an authentication ticket from the distributed cache and deletes its tracking row.
    /// Used for logout, and by the cookie handler when it discards a ticket that has expired.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAsync(string key)
    {
        // Untracking belongs here rather than in an OnSigningOut handler: the cookie handler passes the
        // session key straight to this method, whereas CookieSigningOutContext does not carry it and an
        // event could only recover it via HttpContext.AuthenticateAsync — which deadlocks when sign-out is
        // raised from inside the handler's own authentication pass. Doing it here also covers the handler's
        // expired-ticket path, which removes the ticket without ever raising OnSigningOut.
        //
        // Delete the tracking row BEFORE removing the cached ticket, for the reason spelled out in
        // SessionManager.RevokeSessionsCoreAsync: a concurrent sliding renewal re-writes its ticket
        // unconditionally and only then extends its row, so with the row already gone that extension
        // reports "no row" and the renewal removes its own ticket (see RenewAsync below). The opposite
        // order leaves a window — ticket removed, row still present — in which a renewal both resurrects
        // the ticket and extends the row successfully, and the untracking below then strips the tracking
        // row off a session whose ticket is still live: invisible to listing, unreachable by revocation.
        try
        {
            await this.activeSessionTracker.UntrackAsync(key);
        }
        catch (Exception ex)
        {
            // A failed row delete must not fail the sign-out (or the authentication pass that discarded an
            // expired ticket), and must not skip the ticket removal below — the cached ticket is what keeps
            // authorizing requests. The row is left for CleanupExpiredSessionsAsync to reap.
            this.logger.LogWarning(ex, "Failed to remove the tracked session row for session {SessionTag}", SessionTag(key));
        }

        await this.ticketCacheService.RemoveTicketAsync(key);
    }

    /// <summary>
    /// Renews an authentication ticket by updating it in the cache and extending the tracked
    /// session row's expiration to match. Used for sliding expiration.
    /// </summary>
    /// <param name="key">The session key that identifies the ticket.</param>
    /// <param name="ticket">The updated authentication ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        // Sliding renewal computes and assigns a fresh ExpiresUtc before calling RenewAsync, so a null here
        // means the authentication pipeline is misconfigured rather than a state to paper over.
        var expiresUtc = ticket.Properties.ExpiresUtc
            ?? throw new InvalidOperationException(
                "Authentication ticket has no ExpiresUtc when renewing the session. "
                + "Tickets must reach the store through CookieAuthenticationHandler, which assigns the renewed ExpiresUtc before RenewAsync runs.");

        await this.ticketCacheService.SetTicketAsync(key, ticket, expiresUtc);

        // Keep the tracked session row's ExpiresAt in step with the renewed ticket. Otherwise a
        // long-lived sliding session outlives its row and disappears from session listing,
        // revocation, and refresh — so an authorization change (e.g. a demotion) would never
        // reach a user who simply stays active.
        bool sessionStillTracked;
        try
        {
            sessionStillTracked = await this.activeSessionTracker.ExtendAsync(key, expiresUtc);
        }
        catch (Exception ex)
        {
            // A failed row update must not fail the renewal itself: the cache ticket is already
            // renewed, and the row merely lags (the pre-extension behavior) until the next
            // successful renewal. Only a definitive "no row" answer (below) is treated as revoked.
            this.logger.LogWarning(ex, "Failed to extend tracked session expiration for session {SessionTag}", SessionTag(key));
            return;
        }

        // No tracking row means the session was revoked (or its lagging row was cleaned up as expired)
        // while this renewal was in flight. The SetTicketAsync above has just resurrected the ticket of
        // a session that no longer exists administratively: it would be invisible to session listing,
        // unreachable by revocation and refresh, and would keep sliding forever. Fail closed by removing
        // the ticket again — at worst a legitimately active user has to sign in again.
        if (!sessionStillTracked)
        {
            await this.ticketCacheService.RemoveTicketAsync(key);
            this.logger.LogWarning("Session {SessionTag} was renewed concurrently with its revocation; the renewed ticket has been removed.", SessionTag(key));
        }
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
}
