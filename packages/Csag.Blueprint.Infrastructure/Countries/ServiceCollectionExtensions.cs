namespace Csag.Blueprint.Infrastructure.Countries;

using Csag.Blueprint.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for country catalog registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="NagerCountryCatalog"/> as the singleton <see cref="ICountryCatalog"/>, unless one is already registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintCountries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ICountryCatalog, NagerCountryCatalog>();

        return services;
    }
}
