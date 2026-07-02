namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Executes table view queries with filtering, sorting, and pagination.
/// </summary>
public sealed class TableViewExecutor : ITableViewExecutor
{
    /// <inheritdoc/>
    public Task<(IList<TDto> Data, int TotalCount)> ExecuteAsync<TEntity, TDto>(
        TableViewQueryRequest<TEntity, TDto> request,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TDto : class, new()
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Query);
        ArgumentNullException.ThrowIfNull(request.Definition);
        return ExecuteInternalAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IList<TDto>> ExecuteWithoutPaginationAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ITableViewDefinition<TEntity, TDto> definition,
        Dictionary<string, string>? filters,
        IList<SortColumn>? sortColumns,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TDto : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        return ExecuteWithoutPaginationInternalAsync(query, definition, filters, sortColumns, cancellationToken);
    }

    private static async Task<(IList<TDto> Data, int TotalCount)> ExecuteInternalAsync<TEntity, TDto>(
        TableViewQueryRequest<TEntity, TDto> request,
        CancellationToken cancellationToken)
        where TEntity : class
        where TDto : class, new()
    {
        var query = request.Query;
        var definition = request.Definition;

        query = ApplyFilters(query, definition, request.Filters);

        // Get total count after filtering
        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, definition, request.SortColumns);

        // Apply pagination
        var skip = (request.Page - 1) * request.PageSize;
        query = query.Skip(skip).Take(request.PageSize);

        // Project to DTO
        var data = await query.Select(definition.Projection).ToListAsync(cancellationToken);

        return (data, totalCount);
    }

    private static async Task<IList<TDto>> ExecuteWithoutPaginationInternalAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        ITableViewDefinition<TEntity, TDto> definition,
        Dictionary<string, string>? filters,
        IList<SortColumn>? sortColumns,
        CancellationToken cancellationToken)
        where TEntity : class
        where TDto : class, new()
    {
        query = ApplyFilters(query, definition, filters);
        query = ApplySorting(query, definition, sortColumns);

        var data = await query.Select(definition.Projection).ToListAsync(cancellationToken);
        return data;
    }

    private static IQueryable<TEntity> ApplyFilters<TEntity, TDto>(
        IQueryable<TEntity> query,
        ITableViewDefinition<TEntity, TDto> definition,
        Dictionary<string, string>? filters)
        where TEntity : class
        where TDto : class, new()
    {
        if (filters == null)
        {
            return query;
        }

        foreach (var (columnName, filterValue) in filters)
        {
            if (string.IsNullOrWhiteSpace(filterValue))
            {
                continue;
            }

            var filterExpression = definition.GetFilterExpression(columnName, filterValue);
            if (filterExpression != null)
            {
                query = query.Where(filterExpression);
            }
        }

        return query;
    }

    private static IQueryable<TEntity> ApplySorting<TEntity, TDto>(
        IQueryable<TEntity> query,
        ITableViewDefinition<TEntity, TDto> definition,
        IList<SortColumn>? sortColumns)
        where TEntity : class
        where TDto : class, new()
    {
        if (sortColumns == null || sortColumns.Count == 0)
        {
            return query;
        }

        IOrderedQueryable<TEntity>? ordered = null;

        foreach (var sort in sortColumns)
        {
            if (string.IsNullOrWhiteSpace(sort.ColumnName))
            {
                continue;
            }

            var sortExpression = definition.GetSortExpression(sort.ColumnName);
            if (sortExpression == null)
            {
                continue;
            }

            ordered = ApplyNextSort(query, ordered, sortExpression.Value.Expression, sort.Direction == SortDirection.Desc);
        }

        return ordered ?? query;
    }

    private static IOrderedQueryable<TEntity> ApplyNextSort<TEntity>(
        IQueryable<TEntity> query,
        IOrderedQueryable<TEntity>? ordered,
        System.Linq.Expressions.LambdaExpression sortExpression,
        bool descending)
        where TEntity : class
    {
        var typed = (dynamic)sortExpression;

        if (ordered == null)
        {
            return descending
                ? Queryable.OrderByDescending(query, typed)
                : Queryable.OrderBy(query, typed);
        }

        return descending
            ? Queryable.ThenByDescending(ordered, typed)
            : Queryable.ThenBy(ordered, typed);
    }
}
