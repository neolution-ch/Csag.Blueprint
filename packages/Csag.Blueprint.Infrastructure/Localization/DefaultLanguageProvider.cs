namespace Csag.Blueprint.Infrastructure.Localization;

using Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Default implementation of <see cref="ICurrentLanguageProvider"/> that returns fixed language codes.
/// This can be used as a fallback or for testing purposes.
/// Applications should typically provide their own implementation that gets language from HTTP context,
/// user preferences, or other sources.
/// </summary>
public sealed class DefaultLanguageProvider : ICurrentLanguageProvider
{
    private readonly string currentLanguageCode;
    private readonly string fallbackLanguageCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLanguageProvider"/> class.
    /// </summary>
    /// <param name="currentLanguageCode">The current language code (defaults to "en").</param>
    /// <param name="fallbackLanguageCode">The fallback language code (defaults to "en").</param>
    public DefaultLanguageProvider(string currentLanguageCode = "en", string fallbackLanguageCode = "en")
    {
        this.currentLanguageCode = currentLanguageCode;
        this.fallbackLanguageCode = fallbackLanguageCode;
    }

    /// <inheritdoc/>
    public string CurrentLanguageCode => this.currentLanguageCode;

    /// <inheritdoc/>
    public string FallbackLanguageCode => this.fallbackLanguageCode;
}
