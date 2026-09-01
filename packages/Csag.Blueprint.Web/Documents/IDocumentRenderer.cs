namespace Csag.Blueprint.Web.Documents;

/// <summary>
/// Interface for rendering documents to an output stream.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Renders a document to the specified output stream.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="output">The output stream to which the rendered document is written.</param>
    void Render(DocumentModel document, Stream output);
}
