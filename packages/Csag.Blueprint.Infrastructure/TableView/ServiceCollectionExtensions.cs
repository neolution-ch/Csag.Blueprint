namespace Csag.Blueprint.Infrastructure.TableView;

using System.Reflection;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for shared table view runtime service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared table view core services required by blueprint infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTableViewCore(this IServiceCollection services)
    {
        services.AddScoped<ITableViewExecutor, TableViewExecutor>();
        services.AddScoped<ITableViewCatalogService, TableViewCatalogService>();

        return services;
    }

    /// <summary>
    /// Discovers and registers concrete subclasses of <see cref="TableViewDefinition{TEntity,TDto}"/>
    /// from the specified assembly into the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="definitionsAssembly">The assembly to scan for table view definitions.</param>
    /// <returns>A <see cref="TableViewDefinitionScanResult"/> containing the discovered definition types.</returns>
    public static TableViewDefinitionScanResult AddBlueprintTableViewDefinitions(
        this IServiceCollection services,
        Assembly definitionsAssembly)
    {
        var definitionTypes = new List<Type>();
        var definitionBaseType = typeof(TableViewDefinition<,>);

        foreach (var type in definitionsAssembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            var concreteBaseType = TableViewReflectionHelper.FindGenericBaseType(type, definitionBaseType);
            if (concreteBaseType == null)
            {
                continue;
            }

            // Register the definition as its concrete type (for DI injection into endpoints)
            services.AddScoped(type);
            definitionTypes.Add(type);
        }

        return new TableViewDefinitionScanResult(definitionTypes);
    }

    /// <summary>
    /// Registers catalog entries for table view definitions that implement <see cref="ITableViewDefinitionInfo"/>.
    /// Creates <see cref="ITableViewCatalogRegistration"/> instances from discovered definition types.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scanResult">The scan result containing discovered definition types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTableViewCatalogRegistrations(
        this IServiceCollection services,
        TableViewDefinitionScanResult scanResult)
    {
        var catalogRegistrationType = typeof(ITableViewCatalogRegistration);
        var definitionInfoType = typeof(ITableViewDefinitionInfo);
        var definitionBaseType = typeof(TableViewDefinition<,>);

        foreach (var type in scanResult.DefinitionTypes)
        {
            // Only process definitions implementing ITableViewDefinitionInfo
            if (!definitionInfoType.IsAssignableFrom(type))
            {
                continue;
            }

            var capturedType = type;
            var concreteBaseType = TableViewReflectionHelper.FindGenericBaseType(type, definitionBaseType);

            if (concreteBaseType == null)
            {
                continue;
            }

            services.AddScoped(catalogRegistrationType, sp =>
            {
                var registration = TableViewReflectionHelper.ExtractCatalogRegistration(capturedType, concreteBaseType);

                if (registration == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to extract catalog registration metadata from type '{capturedType.FullName}'. " +
                        "Ensure the type implements ITableViewDefinitionInfo with valid static properties.");
                }

                return registration;
            });
        }

        return services;
    }

    /// <summary>
    /// Adds all shared table view services in one call: core services, definition scanning,
    /// and catalog registrations. This is the recommended method for consumers who want
    /// the complete table view setup with a single call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="definitionsAssembly">The assembly to scan for table view definitions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTableView(
        this IServiceCollection services,
        Assembly definitionsAssembly)
    {
        services.AddBlueprintTableViewCore();
        var scanResult = services.AddBlueprintTableViewDefinitions(definitionsAssembly);
        services.AddBlueprintTableViewCatalogRegistrations(scanResult);

        return services;
    }

    /// <summary>
    /// Adds the blueprint table view preferences service that manages user-specific
    /// table view layout, filter, and sorting preferences.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <typeparam name="TUser">The user entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintTableViewPreferences<TContext, TUser>(
        this IServiceCollection services)
        where TContext : DbContext
        where TUser : BlueprintUser
    {
        services.AddScoped<ITableViewPreferencesService, BlueprintTableViewPreferencesService<TContext, TUser>>();

        return services;
    }
}
