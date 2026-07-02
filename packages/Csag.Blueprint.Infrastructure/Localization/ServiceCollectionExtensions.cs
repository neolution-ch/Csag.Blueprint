namespace Csag.Blueprint.Infrastructure.Localization;

using Csag.Blueprint.Infrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Extension methods for blueprint database-backed localization service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds database-backed localization services with unified two-tier caching.
    /// Registers <see cref="ITranslationProvider"/>, <see cref="IStringLocalizer"/>,
    /// <see cref="IStringLocalizerFactory"/>, and <see cref="ITranslationCacheInvalidator"/>.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type containing Translation entities.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="defaultLanguage">The default language code (e.g., "en-GB", "de-CH").</param>
    /// <param name="translationDefaults">Dictionary mapping dot-path keys to English default text (i.e. <c>TranslationDefaults.All</c>).</param>
    /// <param name="l1ExpirationMinutes">The L1 (in-memory) cache expiration in minutes.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintDbLocalization<TContext>(
        this IServiceCollection services,
        string defaultLanguage,
        IReadOnlyDictionary<string, string> translationDefaults,
        int l1ExpirationMinutes)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrEmpty(defaultLanguage);
        ArgumentNullException.ThrowIfNull(translationDefaults);

        services.AddMemoryCache();

        services.AddSingleton<ITranslationProvider>(sp =>
            new TranslationProvider<TContext>(
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IDistributedCache<CacheId>>(),
                sp.GetRequiredService<IDbContextFactory<TContext>>(),
                sp.GetRequiredService<ILogger<TranslationProvider<TContext>>>(),
                defaultLanguage,
                translationDefaults,
                l1ExpirationMinutes));

        services.AddSingleton<IStringLocalizerFactory>(sp =>
            new BlueprintDbStringLocalizerFactory(
                sp.GetRequiredService<ITranslationProvider>(),
                sp.GetRequiredService<ILoggerFactory>(),
                translationDefaults));

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IStringLocalizerFactory>();
            return factory.Create(typeof(BlueprintDbStringLocalizer));
        });

        services.AddSingleton<ITranslationCacheInvalidator, TranslationCacheInvalidator>();

        return services;
    }

    /// <summary>
    /// Adds a pass-through localizer for generation mode.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintPassThroughLocalization(this IServiceCollection services)
    {
        var passThrough = new PassThroughStringLocalizer();
        services.AddSingleton<IStringLocalizerFactory>(passThrough);
        services.AddSingleton<IStringLocalizer>(passThrough);
        services.AddSingleton<ITranslationProvider, NoOpTranslationProvider>();
        services.AddMemoryCache();
        services.AddSingleton<ITranslationCacheInvalidator, NoOpTranslationCacheInvalidator>();

        return services;
    }
}
