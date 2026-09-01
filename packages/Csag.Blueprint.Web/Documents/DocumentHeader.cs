namespace Csag.Blueprint.Web.Documents;

/// <summary>
/// Represents the header of a document.
/// </summary>
public sealed class DocumentHeader
{
    /// <summary>
    /// Gets the header title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the header subtitle.
    /// </summary>
    public string? Subtitle { get; init; }
}
