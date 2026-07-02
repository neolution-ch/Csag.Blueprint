namespace Csag.Blueprint.Web.Helpers;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Helper for distributing CSRF request token cookies.
/// Used by <see cref="Middleware.CsrfMiddleware"/> for ongoing requests and by login endpoints
/// to distribute the token immediately upon session creation.
/// </summary>
public static class CsrfTokenDistributor
{
    /// <summary>
    /// Appends the CSRF request token cookie to the response.
    /// Calls <see cref="IAntiforgery.GetAndStoreTokens"/> to generate a new token pair,
    /// then sets the non-HttpOnly cookie so JavaScript can read the token value.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="securitySettings">The security configuration settings.</param>
    public static void DistributeRequestToken(HttpContext context, IAntiforgery antiforgery, SecuritySettings securitySettings)
    {
        var csrfSettings = securitySettings.Csrf;
        if (!csrfSettings.Enabled)
        {
            return;
        }

        var tokens = GetOrRefreshTokens(context, antiforgery, csrfSettings);

        context.Response.Cookies.Append(
            csrfSettings.RequestTokenCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = securitySettings.CookieSecurePolicy switch
                {
                    CookieSecurePolicy.Always => true,
                    CookieSecurePolicy.None => false,
                    _ => context.Request.IsHttps,
                },
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
    }

    /// <summary>
    /// Returns valid antiforgery tokens. If the browser already has an antiforgery cookie,
    /// attempts to reuse it; if the cookie is stale (e.g. Data Protection keys rotated after
    /// a container recreate), generates and stores a fresh token pair.
    /// </summary>
    private static AntiforgeryTokenSet GetOrRefreshTokens(HttpContext context, IAntiforgery antiforgery, CsrfSettings csrfSettings)
    {
        var hasExistingCookie = context.Request.Cookies.ContainsKey(csrfSettings.CookieName);
        if (!hasExistingCookie)
        {
            return antiforgery.GetAndStoreTokens(context);
        }

        var tokens = antiforgery.GetTokens(context);

        // When CookieToken is non-null, the existing cookie was stale or undecryptable
        // (e.g. Data Protection keys changed after a container recreate). The new cookie token
        // was generated in memory but not persisted. Call GetAndStoreTokens to persist it.
        if (tokens.CookieToken is not null)
        {
            return antiforgery.GetAndStoreTokens(context);
        }

        return tokens;
    }
}
