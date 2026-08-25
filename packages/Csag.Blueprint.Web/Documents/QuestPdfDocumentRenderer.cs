namespace Csag.Blueprint.Web.Documents;

using QuestPDF.Fluent;

/// <summary>
/// Renders documents using QuestPDF.
/// </summary>
public sealed class QuestPdfDocumentRenderer : IDocumentRenderer
{
    /// <inheritdoc/>
    public void Render(Document document, Stream output)
    {
        document.GeneratePdf(output);
    }
}
