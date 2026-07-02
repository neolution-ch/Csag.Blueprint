namespace Csag.Blueprint.Infrastructure.Localization;

/// <summary>
/// Holds a merged translation dictionary for a single language with fallbacks applied.
/// </summary>
/// <remarks>
/// Serialized to the distributed cache using MessagePack (contractless resolver).
/// Must have public properties and a public constructor for serialization.
/// </remarks>
public sealed class TranslationSnapshot
{
    /// <summary>
    /// Gets or sets the merged translation dictionary with fallbacks applied.
    /// Keys are dot-path translation keys, values are the resolved translated text.
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>
    /// Gets or sets the latest modification timestamp across all contributing translations.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }
}
