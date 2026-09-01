namespace Csag.Blueprint.Web.Documents;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>
/// Renders documents using QuestPDF.
/// </summary>
public sealed class QuestPdfDocumentRenderer : IDocumentRenderer
{
    /// <inheritdoc/>
    public void Render(DocumentModel document, Stream output)
    {
        var questPdfDocument = CreateDocument(document);

        questPdfDocument.GeneratePdf(output);
    }

    /// <summary>
    /// Creates a QuestPDF document from the specified document model.
    /// </summary>
    /// <param name="document">The document model to convert into a QuestPDF document.</param>
    private static Document CreateDocument(DocumentModel document)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header()
                    .Element(container => RenderHeader(container, document.Header));

                page.Content()
                    .Element(container => RenderContent(container, document.Sections));

                page.Footer()
                    .Element(container => RenderFooter(container, document.Footer));
            });
        });
    }

    /// <summary>
    /// Renders the document header.
    /// </summary>
    private static void RenderHeader(IContainer container, DocumentHeader? header)
    {
        if (header is null)
        {
            return;
        }

        container
            .Column(column =>
            {
                column.Item()
                    .Text(header.Title)
                    .FontSize(26)
                    .Bold();

                column.Item()
                    .PaddingTop(5)
                    .Text(header.Subtitle)
                    .FontSize(12)
                    .FontColor(Colors.Grey.Darken1);
            });
    }

    /// <summary>
    /// Renders the document sections and their items.
    /// </summary>
    private static void RenderContent(IContainer container, IReadOnlyList<DocumentSection> sections)
    {
        container
            .PaddingTop(30)
            .Column(column =>
            {
                foreach (var section in sections)
                {
                    column.Item()
                        .Text(section.Title)
                        .FontSize(16)
                        .Bold();

                    column.Item()
                        .PaddingTop(8)
                        .LineHorizontal(1);

                    for (var i = 0; i < section.Items.Count; i++)
                    {
                        var item = section.Items[i];
                        var paddingTop = i == 0 ? 15 : 8;

                        column.Item()
                            .PaddingTop(paddingTop)
                            .Text($"{item.Label} {item.Value}");
                    }
                }
            });
    }

    /// <summary>
    /// Renders the document footer.
    /// </summary>
    private static void RenderFooter(IContainer container, DocumentFooter? footer)
    {
        if (footer is null)
        {
            return;
        }

        container
            .AlignCenter()
            .Text(footer.Text)
            .FontSize(9)
            .FontColor(Colors.Grey.Darken1);
    }
}
