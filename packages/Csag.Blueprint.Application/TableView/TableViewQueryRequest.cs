namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Represents a request to execute a table view query.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <typeparam name="TDto">The DTO type to project results into.</typeparam>
public sealed class TableViewQueryRequest<TEntity, TDto>
    where TEntity : class
    where TDto : class, new()
{
    /// <summary>
    /// Gets or sets the base queryable to apply filters, sorting, and pagination to.
    /// </summary>
    public required IQueryable<TEntity> Query { get; set; }

    /// <summary>
    /// Gets or sets the table view definition defining available columns and metadata.
    /// </summary>
    public required ITableViewDefinition<TEntity, TDto> Definition { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of filter name to filter value.
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    /// Gets or sets the sort columns to apply, in priority order.
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
