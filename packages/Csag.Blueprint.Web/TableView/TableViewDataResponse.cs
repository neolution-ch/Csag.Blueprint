namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Generic response for table view data containing metadata, paginated data, and pagination info.
/// </summary>
/// <typeparam name="TDto">The DTO type for the data items.</typeparam>
public sealed class TableViewDataResponse<TDto>
    where TDto : class
{
    /// <summary>
    /// Gets or sets the metadata describing all available columns.
    /// </summary>
    public IList<TableViewColumnMetadata> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the data items for the current page.
    /// </summary>
    public IList<TDto> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of items matching the filters.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }
}
