namespace Csag.Blueprint.Web.Documents;

using QuestPDF.Fluent;

/// <summary>
/// Interface for rendering QuestPDF documents to an output stream.
/// Implementations are responsible for converting the document into its final output format.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Renders a QuestPDF document to the specified output stream.
    /// </summary>
    /// <param name="document">The QuestPDF document to render.</param>
    /// <param name="output">The output stream to which the rendered document is written.</param>
    void Render(Document document, Stream output);
}
