namespace Csag.Blueprint.Infrastructure.Localization;

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating <see cref="BlueprintDbStringLocalizer"/> instances.
/// Since translations are global (not per-resource-type), all instances share the same behavior.
/// </summary>
public sealed class BlueprintDbStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ITranslationProvider translationProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly IReadOnlyDictionary<string, string> translationDefaults;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintDbStringLocalizerFactory"/> class.
    /// </summary>
    /// <param name="translationProvider">The translation provider.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="translationDefaults">Dictionary mapping dot-path keys to English default text (i.e. <c>TranslationDefaults.All</c>).</param>
    public BlueprintDbStringLocalizerFactory(
        ITranslationProvider translationProvider,
        ILoggerFactory loggerFactory,
        IReadOnlyDictionary<string, string> translationDefaults)
    {
        this.translationProvider = translationProvider;
        this.loggerFactory = loggerFactory;
        this.translationDefaults = translationDefaults;
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(Type resourceSource)
    {
        return new BlueprintDbStringLocalizer(
            this.translationProvider,
            this.loggerFactory.CreateLogger<BlueprintDbStringLocalizer>(),
            this.translationDefaults);
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(string baseName, string location)
    {
        return new BlueprintDbStringLocalizer(
            this.translationProvider,
            this.loggerFactory.CreateLogger<BlueprintDbStringLocalizer>(),
            this.translationDefaults);
    }
}
