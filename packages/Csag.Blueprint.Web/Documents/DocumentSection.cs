namespace Csag.Blueprint.Web.Documents;

/// <summary>
/// Represents a section within a document.
/// </summary>
public sealed class DocumentSection
{
    /// <summary>
    /// Gets the section title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the items contained in the section.
    /// </summary>
    public IReadOnlyList<DocumentItem> Items { get; init; } = [];
}
