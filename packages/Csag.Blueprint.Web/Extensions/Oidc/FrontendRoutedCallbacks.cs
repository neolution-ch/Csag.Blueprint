namespace Csag.Blueprint.Web.Extensions.Oidc;

using Csag.Blueprint.Web.Helpers;
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
    /// <param name="options">The scheme's options, with its callback path already set.</param>
    /// <param name="frontendBaseUrl">The validated frontend base URL.</param>
    public static void Apply(OpenIdConnectOptions options, string frontendBaseUrl)
    {
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
