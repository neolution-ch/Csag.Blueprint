namespace Csag.Blueprint.Web.Documents;

/// <summary>
/// Represents a labelled value within a document section.
/// </summary>
public sealed class DocumentItem
{
    /// <summary>
    /// Gets the item label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets the item value.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
