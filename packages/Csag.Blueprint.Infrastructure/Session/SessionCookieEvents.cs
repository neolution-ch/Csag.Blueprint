namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

/// <summary>
/// The package's handlers for the session cookie's events. <see cref="SessionCookieOptionsSetup"/> installs each one
/// after whatever handler the application registered, so both run.
/// </summary>
internal static class SessionCookieEvents
{
    private static readonly object SigningInKey = new();
    private static readonly object RenewalRefusedKey = new();

    /// <summary>
    /// Answers an unauthenticated request with 401 instead of a redirect to a login page the API does not have.
    /// </summary>
    /// <param name="context">The redirect context.</param>
    /// <returns>A completed task.</returns>
    public static Task RedirectToLoginAsync(RedirectContext<CookieAuthenticationOptions> context)
        => RespondWithStatusAsync(context, StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Answers a forbidden request with 403 instead of a redirect to an access-denied page the API does not have.
    /// </summary>
    /// <param name="context">The redirect context.</param>
    /// <returns>A completed task.</returns>
    public static Task RedirectToAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context)
        => RespondWithStatusAsync(context, StatusCodes.Status403Forbidden);

    /// <summary>
    /// Makes the session cookie persistent, refuses a sign-in for an account that may not hold a session, and
    /// marks the request as signing in for the ticket store.
    /// </summary>
    /// <param name="context">The signing-in context.</param>
    /// <returns>A task that completes once the sign-in may proceed.</returns>
    /// <exception cref="InvalidOperationException">The account may not hold a session.</exception>
    public static async Task SigningInAsync(CookieSigningInContext context)
    {
        // The cookie lives as long as the session's ticket rather than ending with the browser session, so a
        // sign-in never has to state it.
        context.Properties.IsPersistent = true;

        if (SessionPrincipal.TryGetAccountId(context.Principal, out var accountId)
            && !await MayHoldSessionAsync(context.HttpContext, accountId))
        {
            throw new InvalidOperationException(
                $"Account {accountId} may not hold a session: {nameof(ISessionEligibility)} refused the sign-in.");
        }

        context.HttpContext.Items[SigningInKey] = true;
    }

    /// <summary>
    /// Asks whether the account may still hold a session when its session is about to slide. On "no", the renewal
    /// is called off and <see cref="ValidatePrincipalAsync"/>, which runs next in the same authentication pass, ends
    /// the session.
    /// </summary>
    /// <param name="context">The sliding-expiration context.</param>
    /// <returns>A task that completes once the renewal decision is final.</returns>
    public static async Task CheckSlidingExpirationAsync(CookieSlidingExpirationContext context)
    {
        if (!context.ShouldRenew
            || !SessionPrincipal.TryGetAccountId(context.Principal, out var accountId)
            || await MayHoldSessionAsync(context.HttpContext, accountId))
        {
            return;
        }

        context.ShouldRenew = false;
        context.HttpContext.Items[RenewalRefusedKey] = true;
    }

    /// <summary>
    /// Ends the session whose renewal <see cref="CheckSlidingExpirationAsync"/> refused: the request is treated as
    /// unauthenticated, and signing out removes the session and clears the cookie.
    /// </summary>
    /// <param name="context">The validate-principal context.</param>
    /// <returns>A task that completes once the session is ended, if it had to be.</returns>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal is null || !context.HttpContext.Items.ContainsKey(RenewalRefusedKey))
        {
            return;
        }

        context.RejectPrincipal();

        // The sign-out goes through the cookie handler, which removes the ticket and the tracking row through the
        // ticket store. It reuses the cookie read this pass already completed, so it cannot wait on itself.
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    /// <summary>
    /// Tells the ticket store whether the request is signing in, so it can tell a sign-in that reuses a live
    /// session cookie's key apart from a sliding renewal of that session.
    /// </summary>
    /// <param name="httpContext">The request the store was called for, if any.</param>
    /// <returns><see langword="true"/> when the request passed through <see cref="SigningInAsync"/>.</returns>
    public static bool IsSigningIn(HttpContext? httpContext) => httpContext?.Items.ContainsKey(SigningInKey) == true;

    private static Task<bool> MayHoldSessionAsync(HttpContext httpContext, Guid accountId)
        => httpContext.RequestServices.GetRequiredService<ISessionEligibility>().MayHoldSessionAsync(accountId);

    private static Task RespondWithStatusAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        // An application handler that ran first may already have written the response; its status stands then.
        if (!context.Response.HasStarted)
        {
            context.Response.Headers.Remove(HeaderNames.Location);
            context.Response.StatusCode = statusCode;
        }

        return Task.CompletedTask;
    }
}
