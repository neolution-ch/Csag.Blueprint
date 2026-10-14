namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for reusable blueprint session infrastructure registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the blueprint session infrastructure: the ticket store and the session tracking it owns, the
    /// <see cref="ISessionLifecycle"/>, service-account sessions, and the job that deletes expired session rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ticket store, the tracker and the ticket cache are registered with <c>TryAdd</c> under their concrete types
    /// as well as their interfaces, so an application can register its own implementation first, or wrap the
    /// package's: resolve <see cref="DistributedCacheTicketStore"/> or <see cref="ActiveSessionTracker{TContext}"/> as
    /// the inner instance and register the decorator as <see cref="ITicketStore"/> or
    /// <see cref="IActiveSessionTracker"/> after this call.
    /// </para>
    /// <para>
    /// Register <see cref="ISessionRevocationParticipant"/>s and an <see cref="ISessionEligibility"/> as ordinary
    /// services; without an eligibility rule every account may hold a session. Call
    /// <see cref="AddBlueprintCookieSessions"/> as well to make the Identity application cookie the session cookie.
    /// </para>
    /// </remarks>
    /// <typeparam name="TUser">The concrete application user type.</typeparam>
    /// <typeparam name="TContext">The application database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the session settings, for example the client address header or the
    /// expired-session cleanup. The lifetime comes from <c>Blueprint:Security:SessionExpirationHours</c>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintSessionInfrastructure<TUser, TContext>(
        this IServiceCollection services,
        Action<BlueprintSessionOptions>? configure = null)
        where TUser : BlueprintUser
        where TContext : DbContext
    {
        // Clock seam for session creation stamps and expiry comparisons. TryAdd, so a TimeProvider
        // registered before this call (a FakeTimeProvider in tests) wins.
        services.TryAddSingleton(TimeProvider.System);

        // The session lifecycle locates the caller's own session through the current request.
        services.AddHttpContextAccessor();

        var sessionOptions = services.AddOptions<BlueprintSessionOptions>();
        if (configure is not null)
        {
            sessionOptions.Configure(configure);
        }

        sessionOptions.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BlueprintSessionOptions>, BlueprintSessionOptionsValidator>());

        services.TryAddSingleton<ITicketCacheService, TicketCacheService>();
        services.TryAddSingleton<ActiveSessionTracker<TContext>>();
        services.TryAddSingleton<IActiveSessionTracker>(sp => sp.GetRequiredService<ActiveSessionTracker<TContext>>());
        services.TryAddSingleton<DistributedCacheTicketStore>();
        services.TryAddSingleton<ITicketStore>(sp => sp.GetRequiredService<DistributedCacheTicketStore>());

        services.TryAddScoped<SessionManager<TUser, TContext>>();
        services.TryAddScoped<ISessionLifecycle, SessionLifecycle<TUser, TContext>>();
        services.TryAddSingleton<ISessionEligibility, AllowAllSessionEligibility>();
        services.AddHostedService<ExpiredSessionCleanupService<TContext>>();

        // Service-account (reference-style JWT) sessions: revocable, resolved server-side per request.
        // Reuses the same distributed-cache backing store as user sessions (see ServiceAccountSessionManager).
        services.AddScoped<IServiceAccountSessionManager, ServiceAccountSessionManager<TContext>>();

        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(sp => new PostConfigureCookieAuthenticationOptions(sp.GetRequiredService<ITicketStore>()));

        return services;
    }

    /// <summary>
    /// Makes the Identity application cookie the session cookie, with the package's fixed behaviour: the session is
    /// stored server-side by the ticket store and tracked; the cookie is HTTP-only and persistent; its lifetime is
    /// <see cref="BlueprintSessionOptions.Lifetime"/> with sliding expiration; unauthenticated and forbidden requests
    /// get 401 and 403 instead of redirects; Identity's security-stamp validation is off; and
    /// <see cref="ISessionEligibility"/> is asked at sign-in and at every sliding renewal. Requires
    /// <see cref="AddBlueprintSessionInfrastructure{TUser, TContext}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is applied in a post-configure step for <c>IdentityConstants.ApplicationScheme</c>, after every
    /// <c>Configure</c> — <c>AddIdentity</c>'s included — in either registration order. <paramref name="configureCookie"/>
    /// runs first in that step; the package then re-applies <c>HttpOnly</c>, the ticket store, the lifetime and
    /// sliding expiration.
    /// </para>
    /// <para>
    /// Cookie event handlers the application assigns on <see cref="CookieAuthenticationOptions.Events"/> keep running:
    /// the package's handlers run after them. Two are dropped: the framework's own redirect handlers, which the
    /// package's status codes replace, and Identity's security-stamp validator. A scheme that sets <c>EventsType</c>,
    /// or whose events object overrides the methods the package relies on, would skip the package's handlers, and
    /// fails when its options are built.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureCookie">Shapes the cookie itself: typically its name, <c>SameSite</c> mode and secure
    /// policy.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintCookieSessions(this IServiceCollection services, Action<CookieBuilder> configureCookie)
    {
        ArgumentNullException.ThrowIfNull(configureCookie);

        services.Configure<SessionCookieCallbacks>(callbacks => callbacks.ConfigureCookie.Add(configureCookie));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, SessionCookieOptionsSetup>());

        return services;
    }
}
