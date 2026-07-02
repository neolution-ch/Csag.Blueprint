namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Request for table view data export with filtering and sorting (no pagination).
/// </summary>
public class TableViewExportRequest
{
    /// <summary>
    /// Gets or sets the filters to apply. Key is the column name, value is the filter value.
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    /// Gets or sets the sort columns to apply, in priority order.
    /// </summary>
    public IList<SortColumn>? SortColumns { get; set; }

    /// <summary>
    /// Gets or sets the optional page number (1-based). When set together with <see cref="PageSize"/>,
    /// only the specified page is exported instead of all records.
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Gets or sets the optional page size. When set together with <see cref="Page"/>,
    /// only the specified page is exported instead of all records.
    /// </summary>
    public int? PageSize { get; set; }
}
