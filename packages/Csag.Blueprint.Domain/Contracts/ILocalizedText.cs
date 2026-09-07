namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that represent localized text content.
/// These entities store text values for different languages and are typically used
/// as the target of relationships from entities that implement <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
/// </summary>
public interface ILocalizedText
{
    /// <summary>
    /// Gets or sets the text content in the specific language.
    /// </summary>
    string Text { get; set; }

    /// <summary>
    /// Gets or sets the language code for this text (e.g., "en-GB", "de-CH", "fr-CH").
    /// Should follow standard culture codes with language-country format (RFC 4646/BCP 47).
    /// </summary>
    string LanguageCode { get; set; }
}
