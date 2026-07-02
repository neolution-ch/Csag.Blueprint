namespace Csag.Blueprint.Domain.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Represents a translation entry for a specific key and language.
/// This is a reusable platform entity owned by blueprint localization infrastructure.
/// </summary>
public sealed class BlueprintTranslation : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for this translation entry.
    /// </summary>
    public Guid TranslationId { get; set; }

    /// <summary>
    /// Gets or sets the translation key (dot-separated path, e.g., "Validation.EmailRequired").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language code (e.g., "en-GB", "de-CH").
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the translated text. Null indicates the translation has not been provided yet.
    /// </summary>
    public string? Value { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? UpdatedAt { get; set; }
}
