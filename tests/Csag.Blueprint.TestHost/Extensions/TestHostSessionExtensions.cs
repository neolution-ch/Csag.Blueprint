namespace Csag.Blueprint.TestHost.Extensions;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authorization;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Registers distributed session authentication: Identity cookies backed by the Blueprint ticket
/// store (reference-token pattern — the cookie carries only a session key, the ticket with all
/// claims lives server-side in the distributed cache), session tracking, and the permission claims
/// transformation driven by <see cref="TestRolePermissionResolver"/>.
/// </summary>
public static class TestHostSessionExtensions
{
    /// <summary>
    /// Adds the Blueprint session infrastructure (ticket cache, ticket store, session manager),
    /// the role-to-permission resolver and claims transformation, and configures the Identity
    /// application cookie for API usage (401/403 instead of redirects, sliding expiration).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cookieSecurePolicy">The cookie secure policy for the session cookie.</param>
    /// <param name="sessionLifetime">
    /// The session lifetime driving both the cookie <see cref="CookieAuthenticationOptions.ExpireTimeSpan"/>
    /// and the sign-in ticket expiration, so the two can never drift.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostSessionAuthentication(
        this IServiceCollection services, CookieSecurePolicy cookieSecurePolicy, TimeSpan sessionLifetime)
    {
        services.AddBlueprintSessionInfrastructure<TestUser, TestDbContext>();

        // Stateless resolver mapping the shared test roles to their permissions; singleton on purpose.
        services.AddSingleton<IRolePermissionResolver, TestRolePermissionResolver>();

        // Adds permission claims derived from role claims after authentication on every request.
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".AspNetCore.Identity.Application";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;

            // Sliding idle timeout; the login endpoint sets the ticket ExpiresUtc from the same
            // configured value.
            options.ExpireTimeSpan = sessionLifetime;
            options.SlidingExpiration = true;

            // JSON API: return status codes instead of login-page redirects.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };

            // Track/untrack sessions in the BlueprintActiveSessions table on sign-in/sign-out.
            options.Events.OnSignedIn = context => OnSignedInAsync(context, sessionLifetime);
            options.Events.OnSigningOut = OnSigningOutAsync;
        });

        // Disable Identity's SecurityStamp re-validation on the application cookie. The principal
        // built by AuthenticatedPrincipalBuilder intentionally carries no SecurityStamp claim, so
        // the default validator would sign every session out once its 30-minute validation interval
        // elapses. Revocation is handled server-side instead: deleting the cached ticket makes the
        // ticket store return null and the request 401s immediately. PostConfigure runs after every
        // Configure action (including AddIdentity's own), so this no-op cannot be clobbered by
        // registration order.
        services.PostConfigure<CookieAuthenticationOptions>(
            IdentityConstants.ApplicationScheme,
            options => options.Events.OnValidatePrincipal = _ => Task.CompletedTask);

        return services;
    }

    /// <summary>
    /// Tracks the new session in the database so it shows up in the active-sessions administration
    /// surface and can be revoked server-side.
    /// </summary>
    /// <param name="context">The cookie signed-in context.</param>
    /// <param name="sessionLifetime">Fallback lifetime used only if a sign-in did not set an explicit expiration.</param>
    private static async Task OnSignedInAsync(CookieSignedInContext context, TimeSpan sessionLifetime)
    {
        context.Properties.Items.TryGetValue(SessionConstants.SessionKeyPropertyName, out var sessionKey);
        if (string.IsNullOrEmpty(sessionKey))
        {
            return;
        }

        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, CultureInfo.InvariantCulture, out var userId))
        {
            return;
        }

        Guid? currentTenantId = null;
        var tenantIdValue = context.Principal?.FindFirstValue(IdentityClaimTypes.TenantId);
        if (Guid.TryParse(tenantIdValue, CultureInfo.InvariantCulture, out var tenantId))
        {
            currentTenantId = tenantId;
        }

        var sessionManager = context.HttpContext.RequestServices.GetRequiredService<ISessionManager>();
        await sessionManager.TrackSessionAsync(
            userId,
            sessionKey,
            context.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(sessionLifetime),
            context.Request.Headers.UserAgent.ToString(),
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            currentTenantId);
    }

    /// <summary>
    /// Removes the session's tracking row when the user signs out.
    /// </summary>
    /// <param name="context">The cookie signing-out context.</param>
    private static async Task OnSigningOutAsync(CookieSigningOutContext context)
    {
        var authenticateResult = await context.HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Properties == null)
        {
            return;
        }

        authenticateResult.Properties.Items.TryGetValue(SessionConstants.SessionKeyPropertyName, out var sessionKey);
        if (string.IsNullOrEmpty(sessionKey))
        {
            return;
        }

        var sessionManager = context.HttpContext.RequestServices.GetRequiredService<ISessionManager>();
        await sessionManager.UntrackSessionAsync(sessionKey);
    }
}
