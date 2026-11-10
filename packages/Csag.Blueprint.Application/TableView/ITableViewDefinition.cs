namespace Csag.Blueprint.Application.TableView;

using System.Linq.Expressions;

/// <summary>
/// Defines a contract for table view definitions.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type.</typeparam>
public interface ITableViewDefinition<TEntity, TDto>
    where TEntity : class
    where TDto : class
{
    /// <summary>
    /// Gets the metadata for all configured columns.
    /// </summary>
    IList<TableViewColumnMetadata> Metadata { get; }

    /// <summary>
    /// Gets the projection expression to map from entity to DTO.
    /// </summary>
    Expression<Func<TEntity, TDto>> Projection { get; }

    /// <summary>
    /// Gets the filter expression for a specific column.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <param name="filterValue">The filter value as a string.</param>
    /// <param name="timeZone">
    /// The zone that filters on instant (<see cref="DateTimeOffset"/>) columns are evaluated in: a date range
    /// spans the days as they begin and end in this zone, and an instant given without an offset is read in it.
    /// Filters on other columns ignore it.
    /// </param>
    /// <returns>The filter expression, or null if the column is not filterable or the value is malformed.</returns>
    Expression<Func<TEntity, bool>>? GetFilterExpression(string columnName, string filterValue, TimeZoneInfo timeZone);

    /// <summary>
    /// Gets the sort expression for a specific column.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <returns>A tuple containing the sort expression and whether it's computed, or null if the column is not sortable.</returns>
    (LambdaExpression Expression, bool IsComputed)? GetSortExpression(string columnName);
}
