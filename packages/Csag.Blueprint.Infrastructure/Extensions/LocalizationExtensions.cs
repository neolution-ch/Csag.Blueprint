namespace Csag.Blueprint.Infrastructure.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for querying entities with localized text content.
/// These extensions help include and filter localized texts based on the current language context.
/// <para>
/// The fallback ranking is the same everywhere: exact current language, then the same language in a
/// different region (including the bare language code, so "de" matches a current language of "de-CH"),
/// then the exact fallback language, then the same language as the fallback.
/// Texts in an unrelated language are never returned. <see cref="CurrentLanguageTextExpression{TEntity, TLocalizedText}"/>
/// evaluates that ranking in SQL, <see cref="GetCurrentLanguageText{TLocalizedText}(ICollection{TLocalizedText}, ICurrentLanguageProvider)"/>
/// evaluates it in memory on an already-loaded collection.
/// </para>
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// Includes only the single best-matching localized text per entity, using the standard fallback
    /// ranking. After the query runs, <c>LocalizedTexts</c> holds at most one element.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="query">The query to extend.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>Query including at most one localized text per entity.</returns>
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    [SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    public static IQueryable<TEntity> IncludeCurrentLanguageText<TEntity, TLocalizedText>(
        this IQueryable<TEntity> query,
        ICurrentLanguageProvider languageProvider)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        ArgumentNullException.ThrowIfNull(languageProvider);

        var currentLanguage = languageProvider.CurrentLanguageCode;
        var fallbackLanguage = languageProvider.FallbackLanguageCode;
        var currentLanguagePart = LanguagePart(currentLanguage);
        var fallbackLanguagePart = LanguagePart(fallbackLanguage);
        var currentRegionalPrefix = currentLanguagePart + "-";
        var fallbackRegionalPrefix = fallbackLanguagePart + "-";

        return query.Include(e => e.LocalizedTexts
            .Where(t =>
                t.LanguageCode == currentLanguagePart ||
                t.LanguageCode == fallbackLanguagePart ||
                t.LanguageCode.StartsWith(currentRegionalPrefix) ||
                t.LanguageCode.StartsWith(fallbackRegionalPrefix))
            .OrderByDescending(t => t.LanguageCode == currentLanguage)
            .ThenByDescending(t => t.LanguageCode == currentLanguagePart || t.LanguageCode.StartsWith(currentRegionalPrefix))
            .ThenByDescending(t => t.LanguageCode == fallbackLanguage)
            .ThenBy(t => t.LanguageCode)
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

        var currentLanguagePart = LanguagePart(currentLanguage);
        var fallbackLanguagePart = LanguagePart(fallbackLanguage);

        // Priority 1: Exact match for current language
        var exactCurrent = localizedTexts.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, currentLanguage, StringComparison.OrdinalIgnoreCase));
        if (exactCurrent != null)
        {
            return exactCurrent;
        }

        // Priority 2: Same language as current (e.g., "de" or "de-DE" when current is "de-CH")
        var sameLanguageAsCurrent = localizedTexts.FirstOrDefault(t => IsSameLanguage(t.LanguageCode, currentLanguagePart));
        if (sameLanguageAsCurrent != null)
        {
            return sameLanguageAsCurrent;
        }

        // Priority 3: Exact match for fallback language
        var exactFallback = localizedTexts.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, fallbackLanguage, StringComparison.OrdinalIgnoreCase));
        if (exactFallback != null)
        {
            return exactFallback;
        }

        // Priority 4: Same language as fallback (e.g., "en" or "en-US" when fallback is "en-GB")
        return localizedTexts.FirstOrDefault(t => IsSameLanguage(t.LanguageCode, fallbackLanguagePart));
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

    /// <summary>
    /// Builds the selector that resolves the best-matching localized text for the current language.
    /// The returned expression is composed of plain LINQ operators, so EF Core translates it into a
    /// correlated subquery — no texts are loaded into memory and no client evaluation happens.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <returns>An expression selecting the best matching text, or null when no language matches.</returns>
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    [SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness", Justification = "StringComparison overloads cannot be translated to SQL by EF Core.")]
    public static Expression<Func<TEntity, string?>> CurrentLanguageTextExpression<TEntity, TLocalizedText>(
        ICurrentLanguageProvider languageProvider)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        ArgumentNullException.ThrowIfNull(languageProvider);

        var currentLanguage = languageProvider.CurrentLanguageCode;
        var fallbackLanguage = languageProvider.FallbackLanguageCode;
        var currentLanguagePart = LanguagePart(currentLanguage);
        var fallbackLanguagePart = LanguagePart(fallbackLanguage);
        var currentRegionalPrefix = currentLanguagePart + "-";
        var fallbackRegionalPrefix = fallbackLanguagePart + "-";

        // Case sensitivity follows the database collation here and OrdinalIgnoreCase in the in-memory
        // overload; both are case-insensitive under the default SQL Server collation.
        return entity => entity.LocalizedTexts
            .Where(t =>
                t.LanguageCode == currentLanguagePart ||
                t.LanguageCode == fallbackLanguagePart ||
                t.LanguageCode.StartsWith(currentRegionalPrefix) ||
                t.LanguageCode.StartsWith(fallbackRegionalPrefix))
            .OrderByDescending(t => t.LanguageCode == currentLanguage)
            .ThenByDescending(t => t.LanguageCode == currentLanguagePart || t.LanguageCode.StartsWith(currentRegionalPrefix))
            .ThenByDescending(t => t.LanguageCode == fallbackLanguage)
            .ThenBy(t => t.LanguageCode)
            .Select(t => t.Text)
            .FirstOrDefault();
    }

    /// <summary>
    /// Projects each entity together with its best-matching localized text, in a single translated
    /// query. Use this instead of hand-writing the fallback ranking inside a projection.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="query">The query to project.</param>
    /// <param name="languageProvider">Service to determine the current and fallback language codes.</param>
    /// <param name="selector">
    /// Projection receiving the entity and its resolved text. The text argument is null when the
    /// entity has no text in the current or fallback language.
    /// </param>
    /// <returns>The projected query.</returns>
    /// <example>
    /// <code>
    /// var cards = await context.Pedalos
    ///     .SelectWithCurrentLanguageText&lt;Pedalo, PedaloText, CardDto&gt;(
    ///         languageProvider,
    ///         (p, description) =&gt; new CardDto { Id = p.PedaloId, Description = description })
    ///     .ToListAsync(ct);
    /// </code>
    /// </example>
    public static IQueryable<TResult> SelectWithCurrentLanguageText<TEntity, TLocalizedText, TResult>(
        this IQueryable<TEntity> query,
        ICurrentLanguageProvider languageProvider,
        Expression<Func<TEntity, string?, TResult>> selector)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        var textExpression = CurrentLanguageTextExpression<TEntity, TLocalizedText>(languageProvider);
        var entityParameter = selector.Parameters[0];

        // Rebind the ranking onto the selector's entity parameter, then substitute it for the text
        // parameter. Splicing the trees keeps the result a single expression EF Core can translate.
        var textBody = new ParameterSubstitution(textExpression.Parameters[0], entityParameter).Visit(textExpression.Body);
        var body = new ParameterSubstitution(selector.Parameters[1], textBody).Visit(selector.Body);

        return query.Select(Expression.Lambda<Func<TEntity, TResult>>(body, entityParameter));
    }

    /// <summary>
    /// Returns the regional-variant prefix of a language code, e.g. "de-" for both "de" and "de-CH".
    /// Matching against this prefix deliberately excludes the bare language code itself.
    /// </summary>
    /// <param name="languageCode">The language code to reduce.</param>
    /// <returns>The language part followed by a hyphen.</returns>
    private static string LanguagePart(string languageCode)
    {
        var separatorIndex = languageCode.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex < 0 ? languageCode : languageCode[..separatorIndex];
    }

    private static bool IsSameLanguage(string languageCode, string languagePart)
        => string.Equals(languageCode, languagePart, StringComparison.OrdinalIgnoreCase)
            || languageCode.StartsWith(languagePart + "-", StringComparison.OrdinalIgnoreCase);

    private sealed class ParameterSubstitution : ExpressionVisitor
    {
        private readonly ParameterExpression parameter;
        private readonly Expression replacement;

        public ParameterSubstitution(ParameterExpression parameter, Expression replacement)
        {
            this.parameter = parameter;
            this.replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == this.parameter ? this.replacement : base.VisitParameter(node);
    }
}
