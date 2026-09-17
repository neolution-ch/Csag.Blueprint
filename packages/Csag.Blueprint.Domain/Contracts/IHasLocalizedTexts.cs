namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that have localized text content.
/// Implementing entities have a collection of related text entities in different languages.
/// </summary>
/// <typeparam name="TLocalizedText">The type of the localized text entity that implements <see cref="ILocalizedText"/>.</typeparam>
public interface IHasLocalizedTexts<TLocalizedText>
    where TLocalizedText : class, ILocalizedText
{
    /// <summary>
    /// Gets or sets the collection of localized texts for this entity.
    /// Each text entry should have a unique language code within this collection.
    /// </summary>
    ICollection<TLocalizedText> LocalizedTexts { get; set; }
}
