namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Result for table view queries containing metadata, data, and pagination information.
/// </summary>
/// <typeparam name="TDto">The DTO type for the data.</typeparam>
public sealed class TableViewDataResult<TDto>
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
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => this.PageSize > 0 ? (int)Math.Ceiling((double)this.TotalCount / this.PageSize) : 0;
}
