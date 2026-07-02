namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// No-op implementation of <see cref="ITranslationProvider"/> used in generation mode
/// where no real cache or database is available.
/// </summary>
public sealed class NoOpTranslationProvider : ITranslationProvider
{
    /// <inheritdoc/>
    public TranslationSnapshot GetTranslations(string languageCode)
    {
        return new TranslationSnapshot();
    }
}
