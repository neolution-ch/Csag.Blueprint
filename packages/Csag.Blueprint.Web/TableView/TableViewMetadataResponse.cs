namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Shared response for table view metadata.
/// </summary>
public sealed class TableViewMetadataResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the table view.
    /// </summary>
    public string ViewId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the table view.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description for the table view.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column metadata for the table view.
    /// </summary>
    public IList<TableViewColumnMetadata> Columns { get; set; } = [];
}
