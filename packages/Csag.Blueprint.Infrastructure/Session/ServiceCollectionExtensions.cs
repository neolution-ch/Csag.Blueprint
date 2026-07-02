namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for reusable blueprint session infrastructure registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds blueprint session infrastructure services and a generic session manager.
    /// </summary>
    /// <typeparam name="TUser">The concrete application user type.</typeparam>
    /// <typeparam name="TContext">The application database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintSessionInfrastructure<TUser, TContext>(this IServiceCollection services)
        where TUser : BlueprintUser
        where TContext : DbContext
    {
        services.AddSingleton<ITicketCacheService, TicketCacheService>();
        services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();
        services.AddScoped<ISessionManager, SessionManager<TUser, TContext>>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(sp => new PostConfigureCookieAuthenticationOptions(sp.GetRequiredService<ITicketStore>()));

        return services;
    }
}
