namespace Csag.Blueprint.Web.Extensions;

using System.Security.Claims;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for Google OAuth external authentication.
/// </summary>
public static class GoogleOAuthExtensions
{
    /// <summary>
    /// Adds Google OAuth external authentication when enabled in configuration.
    /// Configures the Google authentication scheme to work with the existing cookie-based authentication.
    /// After successful OAuth authentication, users are signed in with Identity cookies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="securitySettings">The validated security settings with replaced secrets.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGoogleOAuthAuthentication(this IServiceCollection services, SecuritySettings securitySettings)
    {
        var googleSettings = securitySettings.OAuth.Google;

        // Only register Google authentication if explicitly enabled
        if (!googleSettings.Enabled)
        {
            return services;
        }

        services
            .AddAuthentication()
            .AddGoogle(options =>
            {
                options.ClientId = googleSettings.ClientId!;
                options.ClientSecret = googleSettings.ClientSecret!;

                // Parse scopes from semicolon-delimited string
                var scopes = googleSettings.Scopes
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();

                // Use External scheme so our custom callback endpoint can handle sign-in
                options.SignInScheme = IdentityConstants.ExternalScheme;

                // Callback path where Google redirects (middleware handles this automatically)
                options.CallbackPath = "/signin-google";

                // Request the configured scopes
                foreach (var scope in scopes)
                {
                    options.Scope.Add(scope);
                }

                // Don't save OAuth tokens in authentication properties (we'll handle this separately if needed)
                options.SaveTokens = false;

                // Map standard OIDC claims to ASP.NET Core Identity claims
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapJsonKey("picture", "picture");
            });

        return services;
    }
}
