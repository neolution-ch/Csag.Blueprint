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

        // Language-level fallback. It never swaps one region for another: "fr-FR" must not
        // silently resolve to "fr-CH" — ask for the exact tag if you want a regional variant.
        // Narrowing is still allowed in either direction, so "de" resolves to "de-CH" and a
        // region-qualified "en-GB" resolves to a bare supported "en".
        try
        {
            var requestedLanguage = new CultureInfo(normalizedRequest).TwoLetterISOLanguageName;
            var languageMatch = supportedCultures.FirstOrDefault(c =>
                string.Equals(c.TwoLetterISOLanguageName, requestedLanguage, StringComparison.OrdinalIgnoreCase)
                && !IsRegionSwap(normalizedRequest, c.Name));
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

        // Language-level fallback, with the same no-region-swap rule as FindMatchingCulture.
        try
        {
            var requestedLanguage = new CultureInfo(normalizedRequest).TwoLetterISOLanguageName;
            var languageMatch = supportedLanguages.FirstOrDefault(l =>
            {
                try
                {
                    var supportedCulture = new CultureInfo(l);
                    return string.Equals(supportedCulture.TwoLetterISOLanguageName, requestedLanguage, StringComparison.OrdinalIgnoreCase)
                        && !IsRegionSwap(normalizedRequest, l);
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

    /// <summary>
    /// Determines whether falling back from <paramref name="requestedTag"/> to
    /// <paramref name="supportedTag"/> would substitute a different region or script — "fr-FR" for
    /// "fr-CH", or Simplified Chinese for Traditional.
    /// </summary>
    /// <remarks>
    /// Only same-language pairs reach this check, so it compares the subtags that remain. Each is
    /// compared only when both sides actually carry it, which keeps a narrowing fallback allowed:
    /// "en-GB" still resolves to a bare "en", and "en-US-u-nu-latn" to "en-US", because the candidate
    /// leaves the subtag unspecified. Comparing whole tags instead would reject both of those, and
    /// would also reject "zh-Hans-CN" against "zh-Hant-CN" as a region swap when the regions are in
    /// fact identical and it is the script that differs.
    /// </remarks>
    /// <param name="requestedTag">The normalized requested culture tag.</param>
    /// <param name="supportedTag">The candidate supported culture tag.</param>
    /// <returns>True if the pair would cross regions or scripts; otherwise, false.</returns>
    private static bool IsRegionSwap(string requestedTag, string supportedTag)
    {
        var requested = ParseSubtags(requestedTag);
        var supported = ParseSubtags(supportedTag);

        return SubtagsConflict(requested.Script, supported.Script)
            || SubtagsConflict(requested.Region, supported.Region);
    }

    /// <summary>
    /// Extracts the BCP 47 script and region subtags from a language tag.
    /// </summary>
    /// <param name="tag">The language tag to parse.</param>
    /// <returns>The script and region subtags, each null when the tag does not carry one.</returns>
    private static (string? Script, string? Region) ParseSubtags(string tag)
    {
        string? script = null;
        string? region = null;
        var parts = tag.Split('-');

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            // A single-character subtag is an extension singleton; nothing after it is script or region.
            if (part.Length == 1)
            {
                break;
            }

            if (script is null && region is null && part.Length == 4 && part.All(char.IsLetter))
            {
                script = part;
            }
            else if (region is null && IsRegionSubtag(part))
            {
                region = part;
            }
            else
            {
                // A variant, a repeated subtag, or anything else we do not match on — skipped.
            }
        }

        return (script, region);
    }

    /// <summary>
    /// Determines whether a subtag is a BCP 47 region: two letters or three digits.
    /// </summary>
    /// <param name="subtag">The subtag to test.</param>
    /// <returns>True when the subtag is shaped like a region.</returns>
    private static bool IsRegionSubtag(string subtag)
        => (subtag.Length == 2 && subtag.All(char.IsLetter))
            || (subtag.Length == 3 && subtag.All(char.IsDigit));

    /// <summary>
    /// Determines whether two optional subtags conflict, treating an absent subtag as compatible.
    /// </summary>
    /// <param name="requested">The requested subtag, if any.</param>
    /// <param name="supported">The supported subtag, if any.</param>
    /// <returns>True when both are present and differ.</returns>
    private static bool SubtagsConflict(string? requested, string? supported)
        => requested is not null
            && supported is not null
            && !string.Equals(requested, supported, StringComparison.OrdinalIgnoreCase);
}
