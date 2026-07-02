namespace Csag.Blueprint.Application.TableView;

using System.Linq.Expressions;
using System.Text.RegularExpressions;

/// <summary>
/// Fluent configuration builder for a table view column.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type.</typeparam>
public sealed class TableViewColumnDefinition<TEntity, TDto>
    where TEntity : class
    where TDto : class
{
    private readonly TableViewColumnMetadata metadata;
    private readonly LambdaExpression? entityExpression;
    private readonly LambdaExpression? dtoExpression;
    private LambdaExpression? sortExpression;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewColumnDefinition{TEntity, TDto}"/> class.
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <param name="dataType">The data type.</param>
    /// <param name="entityExpression">Expression to access the property on the entity.</param>
    /// <param name="dtoExpression">Expression to access the property on the DTO.</param>
    /// <param name="isComputed">Whether this is a computed column.</param>
    internal TableViewColumnDefinition(
        string name,
        string dataType,
        LambdaExpression? entityExpression,
        LambdaExpression? dtoExpression,
        bool isComputed)
    {
        this.metadata = new TableViewColumnMetadata
        {
            Name = name,
            DisplayName = Regex.Replace(name, "(?<!^)([A-Z])", " $1"),
            DataType = dataType,
            IsComputed = isComputed,
        };
        this.entityExpression = entityExpression;
        this.dtoExpression = dtoExpression;
    }

    /// <summary>
    /// Gets the column metadata.
    /// </summary>
    internal TableViewColumnMetadata Metadata => this.metadata;

    /// <summary>
    /// Gets the expression used to read this column from the entity/query source.
    /// This is the value used for filtering and, by default, sorting.
    /// </summary>
    internal LambdaExpression? EntityExpression => this.entityExpression;

    /// <summary>
    /// Gets the optional expression used for sorting this column when it differs from the entity expression.
    /// When this is null, the entity expression is used for sorting.
    /// </summary>
    internal LambdaExpression? SortExpression => this.sortExpression;

    /// <summary>
    /// Gets the expression used to populate the corresponding DTO property during projection.
    /// </summary>
    internal LambdaExpression? DtoExpression => this.dtoExpression;

    /// <summary>
    /// Marks the column as filterable with the specified filter operator.
    /// </summary>
    /// <param name="filterOperator">The filter operator.</param>
    /// <param name="allowedValues">Optional allowed values for enum filters.</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> Filterable(TableViewFilterOperator filterOperator, IEnumerable<string>? allowedValues = null)
    {
        this.metadata.IsFilterable = true;
        this.metadata.FilterOperator = filterOperator;
        this.metadata.AllowedValues = allowedValues;
        this.metadata.FilterInputHint = filterOperator switch
        {
            TableViewFilterOperator.Contains or TableViewFilterOperator.Equals => TableViewFilterInputHint.Text,
            TableViewFilterOperator.Boolean or TableViewFilterOperator.Enum or TableViewFilterOperator.In => TableViewFilterInputHint.Select,
            TableViewFilterOperator.Range => TableViewFilterInputHint.NumberRange,
            TableViewFilterOperator.DateRange => TableViewFilterInputHint.DateRange,
            _ => TableViewFilterInputHint.Text,
        };
        return this;
    }

    /// <summary>
    /// Marks the column as sortable using the configured entity expression.
    /// </summary>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> Sortable()
    {
        this.metadata.IsSortable = true;
        return this;
    }

    /// <summary>
    /// Marks the column as sortable using a strongly typed sort expression.
    /// </summary>
    /// <typeparam name="TProperty">The sort property type.</typeparam>
    /// <param name="sortExpression">The typed sort expression.</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> Sortable<TProperty>(Expression<Func<TEntity, TProperty>> sortExpression)
    {
        ArgumentNullException.ThrowIfNull(sortExpression);

        this.metadata.IsSortable = true;
        this.sortExpression = sortExpression;
        return this;
    }

    /// <summary>
    /// Sets the description for the column.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithDescription(string description)
    {
        this.metadata.Description = description;
        return this;
    }

    /// <summary>
    /// Sets the display name for the column header.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithDisplayName(string displayName)
    {
        this.metadata.DisplayName = displayName;
        return this;
    }

    /// <summary>
    /// Sets the allowed values for enum or In-type filters.
    /// </summary>
    /// <param name="allowedValues">The allowed values.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithAllowedValues(IEnumerable<string> allowedValues)
    {
        this.metadata.AllowedValues = allowedValues;
        return this;
    }

    /// <summary>
    /// Overrides the auto-derived filter input hint for the frontend.
    /// </summary>
    /// <param name="filterInputHint">The input hint to use.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithFilterInputHint(TableViewFilterInputHint filterInputHint)
    {
        this.metadata.FilterInputHint = filterInputHint;
        return this;
    }
}
