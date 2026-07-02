namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Represents a user's saved preferences for a table view.
/// </summary>
public sealed class TableViewPreferencesModel
{
    /// <summary>
    /// Gets or sets the table view identifier.
    /// </summary>
    public string TableViewId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-given name for this saved view.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the user's default view.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the column layout preferences.
    /// </summary>
    public IList<ColumnPreference> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the saved filters.
    /// </summary>
    public Dictionary<string, string> Filters { get; set; } = [];

    /// <summary>
    /// Gets or sets the sort columns to apply, in priority order.
    /// </summary>
    public IList<SortColumn> SortColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets the preferred page size.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets the version marker for the preference schema.
    /// </summary>
    public string Version { get; set; } = "1.0";
}
