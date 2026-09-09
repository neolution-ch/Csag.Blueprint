namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// Canonicalizes language codes for the translation subsystem. <see cref="TranslationProvider{TContext}"/>
/// and <see cref="TranslationCacheInvalidator"/> must agree byte-for-byte on the code used in cache
/// keys and database lookups, so both normalize through this single helper. Canonicalizing up front
/// keeps lookups and the requested-vs-default-language comparison independent of caller casing and
/// database collation; translation rows are expected to store the canonical form.
/// </summary>
internal static class TranslationLanguage
{
    /// <summary>
    /// Normalizes a language code to its canonical form: lowercase invariant.
    /// </summary>
    /// <param name="languageCode">The language code as supplied by the caller (e.g. "de-CH").</param>
    /// <returns>The canonical form (e.g. "de-ch").</returns>
    public static string Normalize(string languageCode)
    {
        ArgumentNullException.ThrowIfNull(languageCode);

        return languageCode.ToLowerInvariant();
    }
}
