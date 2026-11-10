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
    /// Gets or sets the IANA id of the zone that date filters on instant columns are evaluated in,
    /// normally the viewer's zone (in a browser, <c>Intl.DateTimeFormat().resolvedOptions().timeZone</c>).
    /// Optional: when omitted, the tenant's zone is used, and UTC when there is none. A well-formed id that
    /// this server's time-zone data does not know falls back the same way, with a warning in the log. An id
    /// that is not in the IANA form, or is longer than 64 characters, is rejected as a validation error.
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
