namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for anti-forgery (CSRF) protection registration.
/// </summary>
public static class AntiForgeryExtensions
{
    /// <summary>
    /// Adds anti-forgery (CSRF) protection with settings-driven configuration.
    /// The frontend should read the request token cookie and send it as a request header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="securitySettings">The security settings from configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAntiForgeryProtection(this IServiceCollection services, SecuritySettings securitySettings)
    {
        var csrfSettings = securitySettings.Csrf;
        services.AddAntiforgery(options =>
        {
            options.HeaderName = csrfSettings.HeaderName;
            options.Cookie.Name = csrfSettings.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = securitySettings.CookieSecurePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }
}
