namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for shared tenancy runtime service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared tenancy runtime services required by blueprint infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTenancyRuntime(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();

        // Singleton is safe: TenantSaveInterceptor uses TenantContext.Current which is AsyncLocal-based,
        // providing automatic per-request isolation without requiring scoped registration.
        // AsyncLocal flows through async/await and is execution-context-bound, not instance-bound.
        services.AddSingleton<TenantSaveInterceptor>();

        return services;
    }

    /// <summary>
    /// Registers the blueprint <see cref="TenantManager{TUser, TTenant, TContext}"/> as the
    /// <see cref="ITenantManager{TUser, TTenant}"/> implementation. Call this after <see cref="AddBlueprintTenancyRuntime"/>.
    /// </summary>
    /// <typeparam name="TUser">The concrete user type deriving from <see cref="BlueprintUser"/>.</typeparam>
    /// <typeparam name="TTenant">The concrete tenant type deriving from <see cref="BlueprintTenant"/>.</typeparam>
    /// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTenantManager<TUser, TTenant, TContext>(this IServiceCollection services)
        where TUser : BlueprintUser
        where TTenant : BlueprintTenant
        where TContext : DbContext
    {
        services.AddScoped<ITenantManager<TUser, TTenant>, TenantManager<TUser, TTenant, TContext>>();
        return services;
    }
}
