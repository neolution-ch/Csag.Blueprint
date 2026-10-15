namespace Csag.Blueprint.Infrastructure.Session;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// <see cref="HttpContext"/> helpers for the reference-token session model.
/// </summary>
public static class SessionHttpContextExtensions
{
    private static readonly object RewrittenSessionKeyKey = new();

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
