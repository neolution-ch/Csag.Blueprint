namespace Csag.Blueprint.Infrastructure.Session;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// <see cref="HttpContext"/> helpers for the reference-token session model.
/// </summary>
public static class SessionHttpContextExtensions
{
    /// <summary>
    /// Resolves the session key of the request's current authentication session, if any. The key is the
    /// value stored in the authentication ticket properties by <see cref="DistributedCacheTicketStore"/> and
    /// is what <see cref="Csag.Blueprint.Application.Abstractions.Services.ISessionManager"/> methods operate
    /// on. Returns <c>null</c> when the request is not authenticated under the Identity application cookie
    /// (for example, a request carrying only the two-factor scheme, or an anonymous request).
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The current session key, or <c>null</c> if it cannot be determined.</returns>
    public static Task<string?> GetCurrentSessionKeyAsync(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return GetCurrentSessionKeyCoreAsync(httpContext);
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
}
