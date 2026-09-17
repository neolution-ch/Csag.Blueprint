namespace Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Service for determining the current language context.
/// This is used by localization extensions to filter and include appropriate language content.
/// </summary>
public interface ICurrentLanguageProvider
{
    /// <summary>
    /// Gets the current language code (e.g., "en-US", "de-DE").
    /// This may come from HTTP headers, user preferences, session data, or other context.
    /// </summary>
    /// <value>The current language code, or a default language if none is determined.</value>
    string CurrentLanguageCode { get; }

    /// <summary>
    /// Gets the fallback language code used when content is not available in the current language.
    /// </summary>
    /// <value>The fallback language code (typically "en" or "en-US").</value>
    string FallbackLanguageCode { get; }
}
