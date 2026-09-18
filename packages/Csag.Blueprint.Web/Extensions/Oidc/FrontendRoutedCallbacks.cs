namespace Csag.Blueprint.Web.Extensions.Oidc;

using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes a provider's round trip through the frontend origin.
/// </summary>
/// <remarks>
/// The SPA reaches the API only through its own origin's <c>/api/</c> proxy, and behind that proxy the API sees the
/// proxy's upstream host rather than the origin the browser used. The challenge's correlation and nonce cookies are
/// set on that origin, so the provider is told to return there, to a callback path under <c>/api/</c> that the proxy
/// forwards together with those cookies.
/// </remarks>
internal static class FrontendRoutedCallbacks
{
    private const string ExternalAuthFailedError = "external_auth_failed";

    /// <summary>
    /// Sends the provider back to the frontend origin plus the scheme's callback path, and turns a failed round trip
    /// into a redirect back to the application rather than an unhandled exception. The handlers already installed on
    /// <see cref="OpenIdConnectOptions.Events"/> keep running, before this handling; the redirect URI is still the
    /// frontend one whatever they set.
    /// </summary>
    /// <param name="scheme">The authentication scheme the options belong to.</param>
    /// <param name="options">The scheme's options, with its callback path already set.</param>
    /// <param name="frontendBaseUrl">The validated frontend base URL.</param>
    /// <exception cref="InvalidOperationException">
    /// The scheme sets <see cref="AuthenticationSchemeOptions.EventsType"/>, or its final callback path is not under
    /// <see cref="OidcCallbackPaths.ProxiedPrefix"/>.
    /// </exception>
    public static void Apply(string scheme, OpenIdConnectOptions options, string frontendBaseUrl)
    {
        // With EventsType set, the handler resolves its events from DI and never reads Options.Events, so the
        // handlers below would silently not run.
        if (options.EventsType is not null)
        {
            throw new InvalidOperationException(
                $"OpenID Connect provider '{scheme}' sets EventsType, which OAuth.FrontendBaseUrl cannot route through the " +
                "frontend origin. Configure its events on OpenIdConnectOptions.Events instead.");
        }

        // Settings validation covers the configured path; an application Configure can still change the option itself.
        if (!OidcCallbackPaths.IsUnderProxiedPrefix(options.CallbackPath.Value ?? string.Empty))
        {
            throw new InvalidOperationException(
                $"OpenID Connect provider '{scheme}' listens on '{options.CallbackPath}', but with OAuth.FrontendBaseUrl set " +
                $"its callback path must sit under {OidcCallbackPaths.ProxiedPrefix} (no empty, '.' or '..' segments, " +
                "percent-encoding, backslashes, query or fragment), the only paths the frontend origin forwards to the API.");
        }

        var redirectUri = new Uri(frontendBaseUrl).GetLeftPart(UriPartial.Authority) + options.CallbackPath;
        var fallbackFailureRedirect = QueryHelpers.AddQueryString(
            OAuthHelpers.BuildPostAuthRedirect(frontendBaseUrl, "/"),
            OAuthHelpers.ErrorQueryParameter,
            ExternalAuthFailedError);

        var redirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
        options.Events.OnRedirectToIdentityProvider = async context =>
        {
            await redirectToIdentityProvider(context);

            // Assigned after the existing handler so nothing undoes it. The handler stores the value as it stands
            // after this event for the code exchange, so the authorize and token requests send the same redirect_uri.
            context.ProtocolMessage.RedirectUri = redirectUri;
        };

        var remoteFailure = options.Events.OnRemoteFailure;
        options.Events.OnRemoteFailure = async context =>
        {
            await remoteFailure(context);
            if (context.Result is not null)
            {
                return;
            }

            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(FrontendRoutedCallbacks))
                .LogWarning(context.Failure, "External sign-in with {Scheme} failed at the provider callback.", context.Scheme.Name);

            // The challenge's return address is the application's external callback, which answers a missing external
            // login by sending the user back to where they started with an error code. Dropping the external cookie
            // makes sure it finds none, rather than an earlier attempt's identity.
            await context.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var returnAddress = context.Properties?.RedirectUri;
            context.Response.Redirect(returnAddress is not null && IsLocalPath(returnAddress) ? returnAddress : fallbackFailureRedirect);
            context.HandleResponse();
        };
    }

    private static bool IsLocalPath(string path)
    {
        return path.StartsWith('/') && (path.Length == 1 || (path[1] != '/' && path[1] != '\\'));
    }
}
