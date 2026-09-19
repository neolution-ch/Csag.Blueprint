namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for generic OpenID Connect external authentication.
/// </summary>
public static class OidcAuthenticationExtensions
{
    /// <summary>
    /// Registers every enabled OpenID Connect provider from configuration as its own authentication scheme.
    /// Each provider is served by the same generic <c>AddOpenIdConnect</c> handler (no provider-specific
    /// NuGet packages); provider-appropriate defaults are applied by its <see cref="IOidcProviderProfile"/>.
    /// After successful authentication users are handed to the shared external callback endpoint via the
    /// Identity external cookie. When <see cref="OAuthSettings.FrontendBaseUrl"/> is set, every provider returns
    /// the user through that origin (see <see cref="FrontendRoutedCallbacks"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="securitySettings">The validated security settings with replaced secrets.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOidcAuthentication(this IServiceCollection services, SecuritySettings securitySettings)
    {
        var providers = securitySettings.OAuth.Providers;
        var frontendBaseUrl = securitySettings.OAuth.FrontendBaseUrl;

        if (providers == null || providers.Count == 0)
        {
            return services;
        }

        var authBuilder = services.AddAuthentication();

        foreach (var (scheme, provider) in providers)
        {
            // Only register providers that are explicitly enabled.
            if (!provider.Enabled)
            {
                continue;
            }

            var profile = OidcProviderProfileFactory.For(provider.Profile);
            var callbackPath = OidcCallbackPaths.Resolve(scheme, provider);

            authBuilder.AddOpenIdConnect(scheme, options =>
            {
                // CallbackPath depends on the scheme name, so it is resolved here rather than in the profile.
                options.CallbackPath = callbackPath;
                profile.Configure(options, provider);
            });

            // Post-configure steps run after every Configure, so they wrap whatever events the profile and the
            // application installed, in whichever order those were registered.
            services.PostConfigure<OpenIdConnectOptions>(scheme, options => profile.PostConfigure(scheme, options, provider));

            if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                services.PostConfigure<OpenIdConnectOptions>(scheme, options => FrontendRoutedCallbacks.Apply(scheme, options, frontendBaseUrl));
            }
        }

        return services;
    }
}
