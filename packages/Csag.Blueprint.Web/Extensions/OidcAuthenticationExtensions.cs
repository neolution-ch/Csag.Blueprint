namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security;
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
    /// Identity external cookie.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="securitySettings">The validated security settings with replaced secrets.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOidcAuthentication(this IServiceCollection services, SecuritySettings securitySettings)
    {
        var providers = securitySettings.OAuth.Providers;

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
            var callbackPath = string.IsNullOrWhiteSpace(provider.CallbackPath)
                ? $"/signin-oidc/{scheme}"
                : provider.CallbackPath;

            authBuilder.AddOpenIdConnect(scheme, options =>
            {
                // CallbackPath depends on the scheme name, so it is resolved here rather than in the profile.
                options.CallbackPath = callbackPath;
                profile.Configure(options, provider);
            });
        }

        return services;
    }
}
