namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Base request for table view data with filtering, sorting, and pagination.
/// </summary>
public class TableViewDataRequest
{
    /// <summary>
    /// Gets or sets the filters to apply. Key is the column name, value is the filter value.
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    /// Gets or sets the sort columns to apply, in priority order. The first entry becomes the
    /// primary sort, subsequent entries are applied as tie-breakers.
    /// </summary>
    public IList<SortColumn>? SortColumns { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
