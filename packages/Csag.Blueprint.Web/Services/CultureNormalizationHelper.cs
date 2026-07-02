namespace Csag.Blueprint.Web.Services;

using System.Globalization;

/// <summary>
/// Helper class for culture normalization and case-insensitive matching against supported cultures.
/// </summary>
public static class CultureNormalizationHelper
{
    /// <summary>
    /// Finds a matching culture from the supported list using case-insensitive comparison.
    /// First tries exact match, then language-only match (e.g., "de" matches "de-CH").
    /// </summary>
    /// <param name="requestedCulture">The requested culture string.</param>
    /// <param name="supportedCultures">The list of supported cultures.</param>
    /// <returns>The matching culture name, or null if no match found.</returns>
    public static string? FindMatchingCulture(string? requestedCulture, IList<CultureInfo> supportedCultures)
    {
        if (string.IsNullOrWhiteSpace(requestedCulture) || supportedCultures == null || supportedCultures.Count == 0)
        {
            return null;
        }

        var normalizedRequest = requestedCulture.Trim();

        // Exact match (case-insensitive)
        var exactMatch = supportedCultures.FirstOrDefault(c =>
            string.Equals(c.Name, normalizedRequest, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch.Name;
        }

        // Language-only match (e.g., "de" matches "de-CH")
        try
        {
            var requestedLanguage = new CultureInfo(normalizedRequest).TwoLetterISOLanguageName;
            var languageMatch = supportedCultures.FirstOrDefault(c =>
                string.Equals(c.TwoLetterISOLanguageName, requestedLanguage, StringComparison.OrdinalIgnoreCase));
            return languageMatch?.Name;
        }
        catch (CultureNotFoundException)
        {
            // Unrecognized culture — skip it
            return null;
        }
    }

    /// <summary>
    /// Finds a matching culture from the supported language codes using case-insensitive comparison.
    /// First tries exact match, then language-only match.
    /// </summary>
    /// <param name="requestedCulture">The requested culture string.</param>
    /// <param name="supportedLanguages">The list of supported language codes.</param>
    /// <returns>The matching language code, or null if no match found.</returns>
    public static string? FindMatchingLanguage(string? requestedCulture, IList<string> supportedLanguages)
    {
        if (string.IsNullOrWhiteSpace(requestedCulture) || supportedLanguages == null || supportedLanguages.Count == 0)
        {
            return null;
        }

        var normalizedRequest = requestedCulture.Trim();

        // Exact match (case-insensitive)
        var exactMatch = supportedLanguages.FirstOrDefault(l =>
            string.Equals(l, normalizedRequest, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Language-only match (e.g., "de" matches "de-CH")
        try
        {
            var requestedLanguage = new CultureInfo(normalizedRequest).TwoLetterISOLanguageName;
            var languageMatch = supportedLanguages.FirstOrDefault(l =>
            {
                try
                {
                    var supportedCulture = new CultureInfo(l);
                    return string.Equals(supportedCulture.TwoLetterISOLanguageName, requestedLanguage, StringComparison.OrdinalIgnoreCase);
                }
                catch (CultureNotFoundException)
                {
                    return false;
                }
            });
            return languageMatch;
        }
        catch (CultureNotFoundException)
        {
            // Unrecognized culture — skip it
            return null;
        }
    }

    /// <summary>
    /// Validates whether the requested culture is in the supported cultures list (case-insensitive).
    /// </summary>
    /// <param name="requestedCulture">The requested culture string.</param>
    /// <param name="supportedCultures">The list of supported cultures.</param>
    /// <returns>True if the culture is supported; otherwise, false.</returns>
    public static bool IsSupportedCulture(string? requestedCulture, IList<CultureInfo> supportedCultures)
    {
        return FindMatchingCulture(requestedCulture, supportedCultures) != null;
    }

    /// <summary>
    /// Validates whether the requested language is in the supported languages list (case-insensitive).
    /// </summary>
    /// <param name="requestedLanguage">The requested language string.</param>
    /// <param name="supportedLanguages">The list of supported language codes.</param>
    /// <returns>True if the language is supported; otherwise, false.</returns>
    public static bool IsSupportedLanguage(string? requestedLanguage, IList<string> supportedLanguages)
    {
        return FindMatchingLanguage(requestedLanguage, supportedLanguages) != null;
    }
}
