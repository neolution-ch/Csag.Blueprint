namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// Provides merged translation snapshots for a given language, with multi-tier caching.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// Gets the merged translation snapshot for the specified language.
    /// Translations are resolved using the 3-tier fallback: requested language → default language → code defaults.
    /// Results are cached in L1 (in-memory) and L2 (distributed cache).
    /// </summary>
    /// <param name="languageCode">The language code (e.g. "en-GB", "de-CH").</param>
    /// <returns>A snapshot containing the merged translations and the latest modification timestamp.</returns>
    TranslationSnapshot GetTranslations(string languageCode);
}
