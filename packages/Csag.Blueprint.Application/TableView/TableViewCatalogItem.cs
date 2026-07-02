namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Represents an item in the table view catalog.
/// </summary>
public sealed class TableViewCatalogItem
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
    /// Gets or sets the description of the table view.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required permission to access this table view.
    /// </summary>
    public string RequiredPermission { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entity type name this table view represents.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
}
