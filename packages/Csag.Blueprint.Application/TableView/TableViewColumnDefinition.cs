namespace Csag.Blueprint.Application.TableView;

using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
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
    /// <param name="allowedValues">Optional allowed values for enum filters, each served with
    /// itself as its label. When omitted, any allowed values already present on the column (such
    /// as the members auto-derived for enum columns) are kept. To give the values their own labels,
    /// use <see cref="WithAllowedValues(IEnumerable{TableViewAllowedValue})"/>.</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> Filterable(TableViewFilterOperator filterOperator, IEnumerable<string>? allowedValues = null)
    {
        this.metadata.IsFilterable = true;
        this.metadata.FilterOperator = filterOperator;
        if (allowedValues != null)
        {
            this.metadata.AllowedValues = ToSelfLabeledValues(allowedValues);
        }

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
    /// Sets the description for the column. When a translation key is also recorded via
    /// <see cref="WithTranslatedDescription"/>, this value serves as the request-time fallback
    /// for keys that cannot be resolved.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithDescription(string description)
    {
        this.metadata.Description = description;
        return this;
    }

    /// <summary>
    /// Sets the translation key used to localize the column description at request time.
    /// Records the key only — no localizer or database access — so it is safe to call while the
    /// definition is constructed during application startup. When the key cannot be resolved at
    /// request time, the description set via <see cref="WithDescription"/> is served unchanged.
    /// </summary>
    /// <param name="translationKey">The dot-separated translation key (a <c>TranslationKeys</c> constant).</param>
    /// <returns>The column definition for fluent chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithTranslatedDescription(string translationKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(translationKey);

        this.metadata.DescriptionKey = translationKey;
        return this;
    }

    /// <summary>
    /// Sets the display name for the column header. When a translation key is also recorded via
    /// <see cref="WithTranslatedDisplayName"/>, this value serves as the request-time fallback
    /// for keys that cannot be resolved.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithDisplayName(string displayName)
    {
        this.metadata.DisplayName = displayName;
        return this;
    }

    /// <summary>
    /// Sets the translation key used to localize the column header at request time.
    /// Records the key only — no localizer or database access — so it is safe to call while the
    /// definition is constructed during application startup. When the key cannot be resolved at
    /// request time, the display name set via <see cref="WithDisplayName"/> (or the humanized
    /// default derived from the column name) is served unchanged.
    /// </summary>
    /// <param name="translationKey">The dot-separated translation key (a <c>TranslationKeys</c> constant).</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithTranslatedDisplayName(string translationKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(translationKey);

        this.metadata.DisplayNameKey = translationKey;
        return this;
    }

    /// <summary>
    /// Sets the allowed values for enum or In-type filters, each served with itself as its label
    /// and without a translation key. Replaces any allowed values already on the column, including
    /// the members auto-derived for enum columns.
    /// </summary>
    /// <param name="allowedValues">The allowed values.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithAllowedValues(IEnumerable<string> allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);

        this.metadata.AllowedValues = ToSelfLabeledValues(allowedValues);
        return this;
    }

    /// <summary>
    /// Sets the allowed values for enum or In-type filters with their labels and, optionally, the
    /// translation keys that localize those labels at request time. Replaces any allowed values
    /// already on the column, including the members auto-derived for enum columns.
    /// </summary>
    /// <param name="allowedValues">The allowed values.</param>
    /// <returns>This configuration for chaining.</returns>
    public TableViewColumnDefinition<TEntity, TDto> WithAllowedValues(IEnumerable<TableViewAllowedValue> allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);

        var values = allowedValues.ToArray();
        if (Array.Exists(values, value => value is null))
        {
            throw new ArgumentException("Allowed values must not contain null entries.", nameof(allowedValues));
        }

        this.metadata.AllowedValues = Array.AsReadOnly(values);
        return this;
    }

    /// <summary>
    /// Sets the translation key of every allowed value currently on the column to
    /// <c>{prefix}.{Value}</c>, so each label is localized at request time. Records the keys
    /// only — no localizer or database access — so it is safe to call while the definition is
    /// constructed during application startup.
    /// </summary>
    /// <remarks>
    /// Enum columns carry keys under the <c>Enums.{EnumTypeName}</c> prefix by default, for which
    /// a missing translation silently keeps the humanized member name. Keys set here are expected
    /// to exist: when one cannot be resolved, a warning is logged and the label the definition set
    /// is served. Only the values present when this method is called receive keys; allowed values
    /// set afterwards replace them, keys included.
    /// </remarks>
    /// <param name="prefix">The dot-separated key prefix, without a trailing dot.</param>
    /// <returns>This configuration for chaining.</returns>
    /// <exception cref="InvalidOperationException">The column has no allowed values.</exception>
    public TableViewColumnDefinition<TEntity, TDto> WithValueLabelKeyPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        var allowedValues = this.metadata.AllowedValues;
        if (allowedValues is null || allowedValues.Count == 0)
        {
            throw new InvalidOperationException(
                $"Column '{this.metadata.Name}' has no allowed values to assign label keys to. " +
                "Set them with Filterable or WithAllowedValues before calling WithValueLabelKeyPrefix.");
        }

        this.metadata.AllowedValues = Array.AsReadOnly(allowedValues
            .Select(value => value with { LabelKey = $"{prefix}.{value.Value}", IsLabelKeyOptional = false })
            .ToArray());
        return this;
    }

    /// <summary>
    /// Declares that this local reading column's readings are in a zone each row names, not in the tenant's
    /// zone: a place elsewhere, such as a site the tenant runs or a one-off venue, stores its own IANA zone id.
    /// The selected property must be a property of the DTO itself and a column of the same definition, so
    /// every row carries its zone; it is exposed as <see cref="TableViewColumnMetadata.TimeZoneColumn"/>.
    /// Filters are unaffected, because local readings are compared as they read, without a zone.
    /// </summary>
    /// <example>
    /// <code>
    /// Column(dto => dto.OpensLocal)
    ///     .Filterable(TableViewFilterOperator.DateRange)
    ///     .WithTimeZoneFrom(dto => dto.TimeZoneId);
    /// Column(dto => dto.TimeZoneId);
    /// </code>
    /// </example>
    /// <param name="timeZoneSelector">Selects the DTO property that holds the row's IANA zone id.</param>
    /// <returns>This configuration for chaining.</returns>
    /// <exception cref="InvalidOperationException">The column is not a local reading (<see cref="DateTime"/>) column.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="timeZoneSelector"/> does not select a property of the DTO itself (<c>dto => dto.Venue.TimeZoneId</c>
    /// selects one of a nested object).
    /// </exception>
    public TableViewColumnDefinition<TEntity, TDto> WithTimeZoneFrom(Expression<Func<TDto, string?>> timeZoneSelector)
    {
        ArgumentNullException.ThrowIfNull(timeZoneSelector);

        if (this.metadata.DataType != TableViewDataTypes.LocalDateTime)
        {
            throw new InvalidOperationException(
                $"Column '{this.metadata.Name}' is a '{this.metadata.DataType}' column. Only a local reading " +
                "(DateTime) column takes its time zone from its row; an instant needs no zone and a date has none.");
        }

        // Only the property's name is kept, and it names a column of the row; a property of a nested or
        // captured object would name whichever top-level property shares that name.
        if (timeZoneSelector.Body is not MemberExpression { Member: PropertyInfo property } member
            || member.Expression != timeZoneSelector.Parameters[0])
        {
            throw new ArgumentException(
                "The selector must select a property of the DTO itself, such as dto => dto.TimeZoneId, " +
                "not one of a nested or captured object.",
                nameof(timeZoneSelector));
        }

        this.metadata.TimeZoneColumn = property.Name;
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

    private static ReadOnlyCollection<TableViewAllowedValue> ToSelfLabeledValues(IEnumerable<string> allowedValues)
    {
        return Array.AsReadOnly(allowedValues.Select(value => new TableViewAllowedValue(value, value)).ToArray());
    }
}
