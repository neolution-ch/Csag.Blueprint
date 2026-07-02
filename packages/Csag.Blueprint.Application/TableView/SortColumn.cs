namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Represents a single sort key applied to a table view query.
/// Multiple <see cref="SortColumn"/> entries are applied in order: the first becomes
/// the primary sort, subsequent entries become tie-breakers.
/// </summary>
public sealed class SortColumn
{
    /// <summary>
    /// Gets or sets the column name (matches a registered sortable column on the table view definition).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort direction.
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Asc;
}
