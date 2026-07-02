namespace Csag.Blueprint.Infrastructure.Localization;

using Microsoft.Extensions.Localization;

/// <summary>
/// A no-op <see cref="IStringLocalizer"/> used in generation mode (e.g. OpenAPI spec export).
/// Returns the translation key as-is without any formatting or database/cache access.
/// This avoids errors caused by named-placeholder format strings (e.g. <c>{SupportedLanguages}</c>)
/// that are incompatible with <see cref="string.Format(string, object[])"/>.
/// </summary>
public sealed class PassThroughStringLocalizer : IStringLocalizer, IStringLocalizerFactory
{
    /// <inheritdoc />
    public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: true);

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource) => this;

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location) => this;
}
