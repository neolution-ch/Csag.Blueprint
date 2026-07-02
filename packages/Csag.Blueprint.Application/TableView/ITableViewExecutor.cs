namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Defines a contract for executing table view queries with filtering, sorting, and pagination.
/// </summary>
public interface ITableViewExecutor
{
    /// <summary>
    /// Executes a table view query based on the provided request.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TDto">The DTO type to project results into.</typeparam>
    /// <param name="request">The query request containing all parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the data list and total count.</returns>
    Task<(IList<TDto> Data, int TotalCount)> ExecuteAsync<TEntity, TDto>(
        TableViewQueryRequest<TEntity, TDto> request,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TDto : class, new();

    /// <summary>
    /// Executes a table view query without pagination, returning all matching records.
    /// Used for export scenarios where all filtered and sorted data is needed.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TDto">The DTO type to project results into.</typeparam>
    /// <param name="query">The base queryable.</param>
    /// <param name="definition">The table view definition.</param>
    /// <param name="filters">Optional filters to apply.</param>
    /// <param name="sortColumns">Optional sort columns to apply, in priority order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All matching records projected to the DTO type.</returns>
    Task<IList<TDto>> ExecuteWithoutPaginationAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ITableViewDefinition<TEntity, TDto> definition,
        Dictionary<string, string>? filters,
        IList<SortColumn>? sortColumns,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TDto : class, new();
}
