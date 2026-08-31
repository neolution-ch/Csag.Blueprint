namespace Csag.Blueprint.Infrastructure.Extensions;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for querying entities with localized text content.
/// These extensions help include and filter localized texts based on the current language context.
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// Includes localized texts for the current language (with intelligent fallback) for entities
    /// that implement <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// Loads candidate texts including prefix-based fallbacks (e.g., "de-DE" for "de-CH").
    /// Use GetCurrentLanguageText() helper to select the best match from loaded texts.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>Query with localized texts included, filtered by current language with intelligent fallback.</returns>
    public static IQueryable<TEntity> IncludeCurrentLanguageTexts<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        ICurrentLanguageProvider languageProvider)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        var currentLanguage = languageProvider.CurrentLanguageCode;
        var fallbackLanguage = languageProvider.FallbackLanguageCode;
        var exactLanguages = new[] { currentLanguage, fallbackLanguage };

        // Extract language prefixes for prefix-based fallback (e.g., "de" from "de-CH")
        var currentPrefix = currentLanguage.Split('-')[0];
        var fallbackPrefix = fallbackLanguage.Split('-')[0];

        return query.Include(e => e.LocalizedTexts
            .Where(t =>
                exactLanguages.Contains(t.LanguageCode) ||
                t.LanguageCode.StartsWithQuery(currentPrefix + "-") ||
                t.LanguageCode.StartsWithQuery(fallbackPrefix + "-"))
            .OrderByDescending(t => t.LanguageCode == currentLanguage)
            .ThenByDescending(t => t.LanguageCode.StartsWithQuery(currentPrefix + "-"))
            .ThenByDescending(t => t.LanguageCode == fallbackLanguage)
            .ThenByDescending(t => t.LanguageCode.StartsWithQuery(fallbackPrefix + "-"))
            .Take(1));
    }

    /// <summary>
    /// Includes localized texts for a specific language for entities
    /// that implement <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="languageCode">The language code to filter by (e.g., "en-US", "de-DE").</param>
    /// <returns>Query with localized texts included, filtered by the specified language.</returns>
    public static IQueryable<TEntity> IncludeTextsForLanguage<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        string languageCode)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return query.Include(e => e.LocalizedTexts.Where(t => t.LanguageCode == languageCode));
    }

    /// <summary>
    /// Includes localized texts for multiple languages for entities
    /// that implement <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="languageCodes">The language codes to include.</param>
    /// <returns>Query with localized texts included, filtered by the specified languages.</returns>
    public static IQueryable<TEntity> IncludeTextsForLanguages<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        params string[] languageCodes)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return query.Include(e => e.LocalizedTexts.Where(t => languageCodes.Contains(t.LanguageCode)));
    }

    /// <summary>
    /// Includes all localized texts without language filtering for entities
    /// that implement <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <returns>Query with all localized texts included.</returns>
    public static IQueryable<TEntity> IncludeAllTexts<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return query.Include(e => e.LocalizedTexts);
    }

    /// <summary>
    /// Filters entities to only include those that have localized text for the current language.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="languageProvider">Service to determine the current language code.</param>
    /// <returns>Query filtered to entities with text in the current language.</returns>
    public static IQueryable<TEntity> WhereHasCurrentLanguageText<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        ICurrentLanguageProvider languageProvider)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        var currentLanguage = languageProvider.CurrentLanguageCode;
        return query.Where(e => e.LocalizedTexts.Any(t => t.LanguageCode == currentLanguage));
    }

    /// <summary>
    /// Filters entities to only include those that have localized text for a specific language.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="languageCode">The language code to check for.</param>
    /// <returns>Query filtered to entities with text in the specified language.</returns>
    public static IQueryable<TEntity> WhereHasTextForLanguage<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        string languageCode)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return query.Where(e => e.LocalizedTexts.Any(t => t.LanguageCode == languageCode));
    }

    /// <summary>
    /// Gets the localized text for the current language from an entity, with intelligent fallback support.
    /// Prioritizes: 1) Exact current language, 2) Same language prefix, 3) Exact fallback, 4) Fallback prefix.
    /// Use this method on entities that have been loaded with their localized texts.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>The best matching text based on priority, or null if no match exists.</returns>
    public static TLocalizedText? GetCurrentLanguageText<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts,
        ICurrentLanguageProvider languageProvider)
        where TLocalizedText : class, ILocalizedText
    {
        var currentLanguage = languageProvider.CurrentLanguageCode;
        var fallbackLanguage = languageProvider.FallbackLanguageCode;

        // Extract language prefixes (e.g., "de" from "de-CH")
        var currentPrefix = currentLanguage.Split('-')[0];
        var fallbackPrefix = fallbackLanguage.Split('-')[0];

        // Priority 1: Exact match for current language
        var exactCurrent = localizedTexts.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, currentLanguage, StringComparison.OrdinalIgnoreCase));
        if (exactCurrent != null)
        {
            return exactCurrent;
        }

        // Priority 2: Same language prefix as current (e.g., "de-DE" when current is "de-CH")
        var prefixCurrent = localizedTexts.FirstOrDefault(t =>
            t.LanguageCode.StartsWith(currentPrefix + "-", StringComparison.OrdinalIgnoreCase));
        if (prefixCurrent != null)
        {
            return prefixCurrent;
        }

        // Priority 3: Exact match for fallback language
        var exactFallback = localizedTexts.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, fallbackLanguage, StringComparison.OrdinalIgnoreCase));
        if (exactFallback != null)
        {
            return exactFallback;
        }

        // Priority 4: Same language prefix as fallback (e.g., "en-US" when fallback is "en-GB")
        var prefixFallback = localizedTexts.FirstOrDefault(t =>
            t.LanguageCode.StartsWith(fallbackPrefix + "-", StringComparison.OrdinalIgnoreCase));

        return prefixFallback;
    }

    /// <summary>
    /// Gets the localized text for the current language from an entity that exposes localized texts,
    /// with intelligent fallback support.
    /// Prioritizes: 1) Exact current language, 2) Same language prefix, 3) Exact fallback, 4) Fallback prefix.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="entity">The entity containing localized texts.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>The best matching text based on priority, or null if no match exists.</returns>
    public static TLocalizedText? GetCurrentLanguageText<TEntity, TLocalizedText>(
        this TEntity entity,
        ICurrentLanguageProvider languageProvider)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return entity.LocalizedTexts.GetCurrentLanguageText(languageProvider);
    }

    /// <summary>
    /// Gets the localized text for a specific language from an entity.
    /// Use this method on entities that have been loaded with their localized texts.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <param name="languageCode">The language code to get text for.</param>
    /// <returns>The text in the specified language, or null if not available.</returns>
    public static TLocalizedText? GetTextForLanguage<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts,
        string languageCode)
        where TLocalizedText : class, ILocalizedText
    {
        return localizedTexts.FirstOrDefault(t => t.LanguageCode == languageCode);
    }

    /// <summary>
    /// Gets the text content as a string for the current language, with intelligent fallback support.
    /// Prioritizes: 1) Exact current language, 2) Same language prefix, 3) Exact fallback, 4) Fallback prefix.
    /// Use this method on entities that have been loaded with their localized texts.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>The best matching text content, or empty string if no match exists.</returns>
    public static string GetCurrentLanguageTextValue<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts,
        ICurrentLanguageProvider languageProvider)
        where TLocalizedText : class, ILocalizedText
    {
        return localizedTexts.GetCurrentLanguageText(languageProvider)?.Text ?? string.Empty;
    }

    /// <summary>
    /// Gets the text content as a string for a specific language.
    /// Use this method on entities that have been loaded with their localized texts.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="localizedTexts">The collection of localized texts.</param>
    /// <param name="languageCode">The language code to get text for.</param>
    /// <returns>The text content in the specified language, or empty string if not available.</returns>
    public static string GetTextValueForLanguage<TLocalizedText>(
        this ICollection<TLocalizedText> localizedTexts,
        string languageCode)
        where TLocalizedText : class, ILocalizedText
    {
        return localizedTexts.GetTextForLanguage(languageCode)?.Text ?? string.Empty;
    }
}
