namespace Csag.Blueprint.Web.Documents;

/// <summary>
/// Represents a document model that can be rendered by a document renderer.
/// </summary>
public sealed class DocumentModel
{
    /// <summary>
    /// Gets the document header.
    /// </summary>
    public DocumentHeader? Header { get; init; }

    /// <summary>
    /// Gets the sections contained in the document.
    /// </summary>
    public IReadOnlyList<DocumentSection> Sections { get; init; } = [];

    /// <summary>
    /// Gets the document footer.
    /// </summary>
    public DocumentFooter? Footer { get; init; }
}
