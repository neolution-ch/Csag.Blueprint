namespace Csag.Blueprint.Infrastructure.Session;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// <see cref="HttpContext"/> helpers for the reference-token session model.
/// </summary>
public static class SessionHttpContextExtensions
{
    private static readonly object RewrittenSessionKeyKey = new();

    /// <summary>
    /// Signs <paramref name="session"/> in under a new session key. A session the request still holds ends first and
    /// its cookie is replaced, so a copy of that cookie stops working. Use it for every sign-in to the Identity
    /// application cookie, and call it at most once per request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HttpContext.SignInAsync</c> on a request that still carries a live session cookie keeps that cookie's key: the
    /// cookie handler that authenticated the request renews the ticket under the key it read, instead of storing a new
    /// one (<see href="https://github.com/dotnet/aspnetcore/issues/22135">dotnet/aspnetcore#22135</see>). Whoever holds
    /// a copy of the old cookie would then hold the new session, possibly of another account.
    /// </para>
    /// <para>
    /// When the request carries the session cookie, this method signs its session out through that handler, which
    /// removes the old ticket and its tracking row and stops the handler from renewing the old ticket and rewriting the
    /// old cookie when the response starts. It does so whether or not the request authenticated: an application's
    /// <c>OnValidatePrincipal</c> can reject the principal and leave the ticket in place. It then signs in through a
    /// handler instance of its own, which finds the old session gone and stores the ticket under a new key.
    /// </para>
    /// </remarks>
    /// <param name="httpContext">The request signing in.</param>
    /// <param name="session">The principal of the new session.</param>
    /// <param name="properties">The sign-in properties, if any. The session infrastructure sets lifetime and persistence.</param>
    /// <returns>A task that completes once the new session is stored and its cookie written.</returns>
    public static Task SignInNewSessionAsync(this HttpContext httpContext, ClaimsPrincipal session, AuthenticationProperties? properties = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(session);

        return SignInNewSessionCoreAsync(httpContext, session, properties);
    }

    /// <summary>
    /// Resolves the session key of the request's current authentication session, if any. The key is the
    /// value stored in the authentication ticket properties by <see cref="DistributedCacheTicketStore"/>.
    /// Returns <c>null</c> when the request is not authenticated under the Identity application cookie
    /// (for example, a request carrying only the two-factor scheme, or an anonymous request).
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The current session key, or <c>null</c> if it cannot be determined.</returns>
    public static Task<string?> GetCurrentSessionKeyAsync(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return GetCurrentSessionKeyCoreAsync(httpContext);
    }

    /// <summary>
    /// Resolves the request's Identity application cookie session: its key and the account it authenticates.
    /// Returns <c>null</c> when the request has no such session, or the session carries a key no tracked session
    /// could have, so callers never filter or look up by a key that cannot match a tracking row.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The current session, or <c>null</c>.</returns>
    internal static async Task<CurrentSession?> GetCurrentSessionAsync(this HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Properties == null)
        {
            return null;
        }

        authenticateResult.Properties.Items.TryGetValue(SessionConstants.SessionKeyPropertyName, out var sessionKey);
        if (!SessionValueGuards.IsWellFormedSessionKey(sessionKey, SessionConstants.SessionKeyMaxCacheKeyBytes)
            || !SessionPrincipal.TryGetAccountId(authenticateResult.Principal, out var accountId))
        {
            return null;
        }

        return new CurrentSession(sessionKey, accountId);
    }

    /// <summary>
    /// Records that the session lifecycle rewrote the cached ticket of the request's own session, so that
    /// <see cref="DistributedCacheTicketStore"/> keeps the rewrite when the cookie handler renews the session at the
    /// end of the same request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="sessionKey">The key of the session that was rewritten.</param>
    internal static void MarkSessionRewritten(this HttpContext httpContext, string sessionKey)
        => httpContext.Items[RewrittenSessionKeyKey] = sessionKey;

    /// <summary>
    /// Answers whether <see cref="MarkSessionRewritten"/> recorded <paramref name="sessionKey"/> on this request.
    /// </summary>
    /// <param name="httpContext">The request, if any.</param>
    /// <param name="sessionKey">The session key to look for.</param>
    /// <returns><see langword="true"/> when the session lifecycle rewrote that session during the request.</returns>
    internal static bool WasSessionRewritten(this HttpContext? httpContext, string sessionKey)
        => httpContext?.Items[RewrittenSessionKeyKey] is string rewritten && string.Equals(rewritten, sessionKey, StringComparison.Ordinal);

    private static async Task SignInNewSessionCoreAsync(HttpContext httpContext, ClaimsPrincipal session, AuthenticationProperties? properties)
    {
        // Decided by the cookie, not by the authentication result. A rejected principal fails authentication while its
        // ticket stays in the store under the cookie's key, where the handler below would find and reuse it.
        var cookieOptions = httpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        if (cookieOptions.Cookie.Name is { } cookieName
            && !string.IsNullOrEmpty(cookieOptions.CookieManager.GetRequestCookie(httpContext, cookieName)))
        {
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }

        var schemes = httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(IdentityConstants.ApplicationScheme)
            ?? throw new InvalidOperationException($"The {IdentityConstants.ApplicationScheme} authentication scheme is not registered.");

        // Built the way the framework's handler provider builds one. Handlers are registered as transient services, so
        // this is a new instance, not the one HttpContext.SignInAsync would reach.
        var handler = (httpContext.RequestServices.GetService(scheme.HandlerType)
                ?? ActivatorUtilities.CreateInstance(httpContext.RequestServices, scheme.HandlerType))
            as IAuthenticationSignInHandler
            ?? throw new InvalidOperationException($"The {IdentityConstants.ApplicationScheme} authentication handler cannot sign in.");

        await handler.InitializeAsync(scheme, httpContext);
        await handler.SignInAsync(session, properties);
    }

    private static async Task<string?> GetCurrentSessionKeyCoreAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Properties == null)
        {
            return null;
        }

        authenticateResult.Properties.Items.TryGetValue(SessionConstants.SessionKeyPropertyName, out var sessionKey);
        return string.IsNullOrEmpty(sessionKey) ? null : sessionKey;
    }

    /// <summary>
    /// The Identity application cookie session a request authenticated with.
    /// </summary>
    /// <param name="SessionKey">The ticket store key the cookie carries.</param>
    /// <param name="AccountId">The account the session authenticates.</param>
    internal sealed record CurrentSession(string SessionKey, Guid AccountId);
}
