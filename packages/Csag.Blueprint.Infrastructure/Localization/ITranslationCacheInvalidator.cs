namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// Invalidates all translation cache layers for a given language.
/// </summary>
public interface ITranslationCacheInvalidator
{
    /// <summary>
    /// Removes the cached translations for the specified language from all cache layers
    /// (distributed cache and in-process memory cache).
    /// </summary>
    /// <param name="languageCode">The language code whose cache entries should be evicted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateAsync(string languageCode, CancellationToken cancellationToken);
}
