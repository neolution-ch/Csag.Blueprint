namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// No-op implementation of <see cref="ITranslationCacheInvalidator"/> used in generation mode
/// where no real cache is available.
/// </summary>
public sealed class NoOpTranslationCacheInvalidator : ITranslationCacheInvalidator
{
    /// <inheritdoc/>
    public Task InvalidateAsync(string languageCode, CancellationToken cancellationToken) => Task.CompletedTask;
}
