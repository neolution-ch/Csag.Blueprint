namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// Static convenience extensions for localization that use a configurable default language provider.
/// These methods provide simplified usage patterns when dependency injection is not available or desired.
/// </summary>
public static class StaticLocalizationExtensions
{
    private static ICurrentLanguageProvider? defaultProvider;

    /// <summary>
    /// Gets the configured default language provider, or creates a new default one if none is set.
    /// </summary>
    /// <returns>The default language provider.</returns>
    public static ICurrentLanguageProvider DefaultLanguageProvider => defaultProvider ??= new DefaultLanguageProvider();

    /// <summary>
    /// Sets the default language provider for static localization extensions.
    /// This should typically be called once during application startup.
    /// </summary>
    /// <param name="provider">The language provider to use as default.</param>
    public static void SetDefaultLanguageProvider(ICurrentLanguageProvider provider)
    {
        defaultProvider = provider;
    }

    /// <summary>
    /// Convenience method for including localized texts with the current language using the default provider.
    /// This is equivalent to calling IncludeCurrentLanguageTexts with the default language provider.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <returns>Query with localized texts included, filtered by current language with fallback.</returns>
    public static IQueryable<TEntity> IncludeCurrentLanguageText<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return LocalizationExtensions.IncludeCurrentLanguageTexts<TEntity, TLocalizedText>(query, DefaultLanguageProvider);
    }

    /// <summary>
    /// Convenience method for filtering entities that have text in the current language using the default provider.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <returns>Query filtered to entities with text in the current language.</returns>
    public static IQueryable<TEntity> WhereHasCurrentLanguageText<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return LocalizationExtensions.WhereHasCurrentLanguageText<TEntity, TLocalizedText>(query, DefaultLanguageProvider);
    }

    /// <summary>
    /// Convenience method for getting current language text using the default provider.
    /// Implements intelligent fallback with prefix matching.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <returns>The best matching text based on priority, or null if no match exists.</returns>
    public static TLocalizedText? GetCurrentLanguageText<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts)
        where TLocalizedText : class, ILocalizedText
    {
        return localizedTexts.GetCurrentLanguageText(DefaultLanguageProvider);
    }

    /// <summary>
    /// Convenience method for getting current language text from an entity that has localized texts,
    /// using the default provider.
    /// Implements intelligent fallback with prefix matching.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="entity">The entity containing localized texts.</param>
    /// <returns>The best matching text based on priority, or null if no match exists.</returns>
    public static TLocalizedText? GetCurrentLanguageText<TEntity, TLocalizedText>(
        this TEntity entity)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return LocalizationExtensions.GetCurrentLanguageText<TEntity, TLocalizedText>(entity, DefaultLanguageProvider);
    }

    /// <summary>
    /// Convenience method for getting current language text value using the default provider.
    /// Implements intelligent fallback with prefix matching.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <returns>The best matching text content, or empty string if no match exists.</returns>
    public static string GetCurrentLanguageTextValue<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts)
        where TLocalizedText : class, ILocalizedText
    {
        return localizedTexts.GetCurrentLanguageTextValue(DefaultLanguageProvider);
    }
}
