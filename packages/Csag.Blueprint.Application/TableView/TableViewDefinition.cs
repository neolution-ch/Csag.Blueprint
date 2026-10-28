namespace Csag.Blueprint.Application.TableView;

using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Base class for table view definitions that define how entities can be queried and projected to DTOs.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type.</typeparam>
#pragma warning disable S1200 // Classes should not be coupled to too many other classes
public abstract class TableViewDefinition<TEntity, TDto> : ITableViewDefinition<TEntity, TDto>
#pragma warning restore S1200 // Classes should not be coupled to too many other classes
    where TEntity : class
    where TDto : class
{
    private readonly List<TableViewColumnDefinition<TEntity, TDto>> columnDefinitions = [];

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// A column takes its time zone (<see cref="TableViewColumnDefinition{TEntity, TDto}.WithTimeZoneFrom"/>)
    /// from a property that is not a column of this definition.
    /// </exception>
    public IList<TableViewColumnMetadata> Metadata
    {
        get
        {
            // Create metadata list on demand - cannot be cached as columnDefinitions may change during configuration
            var metadata = new List<TableViewColumnMetadata>(this.columnDefinitions.Count);
            foreach (var definition in this.columnDefinitions)
            {
                metadata.Add(definition.Metadata);
            }

            EnsureTimeZoneColumnsExist(metadata);
            return metadata;
        }
    }

    /// <inheritdoc/>
    public Expression<Func<TEntity, TDto>> Projection
    {
        get
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var bindings = new List<MemberBinding>();

            foreach (var config in this.columnDefinitions)
            {
                if (config.DtoExpression == null || config.EntityExpression == null)
                {
                    continue;
                }

                var dtoProperty = GetProperty(config.DtoExpression);
                var entityBody = ReplaceLambdaParameter(config.EntityExpression, parameter);

                // Handle type conversions if needed
                var convertedBody = Expression.Convert(entityBody, dtoProperty.PropertyType);
                bindings.Add(Expression.Bind(dtoProperty, convertedBody));
            }

            var memberInit = Expression.MemberInit(Expression.New(typeof(TDto)), bindings);
            return Expression.Lambda<Func<TEntity, TDto>>(memberInit, parameter);
        }
    }

    /// <summary>
    /// Gets the filter expression for a specific column, evaluating zone-dependent filter values in UTC.
    /// </summary>
    /// <remarks>
    /// Only filters on instant (<see cref="DateTimeOffset"/>) columns depend on a zone: a date range on one
    /// spans whole UTC days here, and an instant given without an offset is read as UTC. Request handling
    /// uses <see cref="GetFilterExpression(string, string, TimeZoneInfo)"/> with the request's effective
    /// zone instead.
    /// </remarks>
    /// <param name="columnName">The column name.</param>
    /// <param name="filterValue">The filter value as a string.</param>
    /// <returns>The filter expression, or null if the column is not filterable or the value is malformed.</returns>
    public Expression<Func<TEntity, bool>>? GetFilterExpression(string columnName, string filterValue)
    {
        return this.GetFilterExpression(columnName, filterValue, TimeZoneInfo.Utc);
    }

    /// <inheritdoc/>
    public Expression<Func<TEntity, bool>>? GetFilterExpression(string columnName, string filterValue, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var config = this.columnDefinitions.FirstOrDefault(c => c.Metadata.Name == columnName);
        if (config?.Metadata.IsFilterable != true || config.EntityExpression == null || config.Metadata.FilterOperator == null)
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");

        // Get the actual underlying type by examining the expression body
        var underlyingType = GetUnderlyingTypeFromExpression(config.EntityExpression);

        // Replace the original lambda parameter with the query parameter without forcing invocation/boxing
        var typedAccess = ReplaceLambdaParameter(config.EntityExpression, parameter);
        Expression? filterExpression = null;

        switch (config.Metadata.FilterOperator.Value)
        {
            case TableViewFilterOperator.Equals:
                filterExpression = BuildEqualsExpression(typedAccess, filterValue, config.Metadata.DataType, timeZone);
                break;

            case TableViewFilterOperator.Contains:
                if (config.Metadata.DataType == TableViewDataTypes.String)
                {
                    var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
                    var valueExpr = Expression.Constant(filterValue);
                    filterExpression = Expression.Call(typedAccess, containsMethod, valueExpr);
                }

                break;

            case TableViewFilterOperator.Boolean:
                if (bool.TryParse(filterValue, out var boolValue))
                {
                    filterExpression = Expression.Equal(typedAccess, Expression.Constant(boolValue, underlyingType));
                }

                break;

            case TableViewFilterOperator.Range:
                filterExpression = BuildRangeExpression(typedAccess, filterValue, config.Metadata.DataType);
                break;

            case TableViewFilterOperator.In:
                filterExpression = BuildInExpression(typedAccess, filterValue, config.Metadata.DataType, timeZone);
                break;

            case TableViewFilterOperator.DateRange:
                filterExpression = BuildDateRangeExpression(typedAccess, filterValue, timeZone);
                break;

            case TableViewFilterOperator.Enum:
                filterExpression = BuildEnumExpression(typedAccess, filterValue);
                break;

            default:
                // Unsupported filter operator - filterExpression remains null
                break;
        }

        return filterExpression != null ? Expression.Lambda<Func<TEntity, bool>>(filterExpression, parameter) : null;
    }

    /// <inheritdoc/>
    public (LambdaExpression Expression, bool IsComputed)? GetSortExpression(string columnName)
    {
        var config = this.columnDefinitions.FirstOrDefault(c => c.Metadata.Name == columnName);
        if (config?.Metadata.IsSortable != true)
        {
            return null;
        }

        var expression = config.SortExpression ?? config.EntityExpression;
        return expression != null ? (expression, config.Metadata.IsComputed) : null;
    }

    /// <summary>
    /// Configures a column that maps directly from an entity property.
    /// </summary>
    /// <remarks>
    /// A column of enum type gets one allowed value per enum member: the member name as the value
    /// (what filters match on), the humanized member name as the label (<c>CreditCard</c> becomes
    /// "Credit card"), and <c>Enums.{EnumTypeName}.{MemberName}</c> as an optional translation key
    /// for the label. Applications translate a label by adding that key to their translation
    /// catalog; without it the humanized label is served, nothing is logged.
    /// <see cref="TableViewColumnDefinition{TEntity, TDto}.WithValueLabelKeyPrefix"/> replaces the
    /// keys, and <c>WithAllowedValues</c> replaces the values.
    /// </remarks>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="dtoPropertySelector">Expression selecting the DTO property.</param>
    /// <param name="entityPropertySelector">Expression selecting the corresponding entity property.</param>
    /// <returns>A column definition for fluent chaining.</returns>
    protected TableViewColumnDefinition<TEntity, TDto> Column<TProperty>(
        Expression<Func<TDto, TProperty>> dtoPropertySelector,
        Expression<Func<TEntity, TProperty>>? entityPropertySelector = null)
    {
        var propertyName = GetPropertyName(dtoPropertySelector);
        var dataType = GetDataType(dtoPropertySelector);

        // If no entity selector provided, assume same property name exists on entity
        var entityExpr = entityPropertySelector ?? CreateEntityExpression(propertyName);

        var config = new TableViewColumnDefinition<TEntity, TDto>(
            propertyName,
            dataType,
            entityExpr,
            dtoPropertySelector,
            isComputed: false);

        if (dataType == TableViewDataTypes.Enum)
        {
            var property = GetProperty(dtoPropertySelector);
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            config.WithAllowedValues(CreateEnumAllowedValues(enumType));
        }

        this.columnDefinitions.Add(config);
        return config;
    }

    /// <summary>
    /// Configures a computed column that is calculated from the entity but doesn't map to a direct property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="columnName">The name of the computed column (should match DTO property name).</param>
    /// <param name="entityExpression">Expression to compute the value from the entity.</param>
    /// <param name="dtoPropertySelector">Expression selecting the DTO property.</param>
    /// <returns>A column definition for fluent chaining.</returns>
    protected TableViewColumnDefinition<TEntity, TDto> ComputedColumn<TProperty>(
        string columnName,
        Expression<Func<TEntity, TProperty>> entityExpression,
        Expression<Func<TDto, TProperty>> dtoPropertySelector)
    {
        var dataType = GetDataType(dtoPropertySelector);

        var config = new TableViewColumnDefinition<TEntity, TDto>(
            columnName,
            dataType,
            entityExpression,
            dtoPropertySelector,
            isComputed: true);

        this.columnDefinitions.Add(config);
        return config;
    }

    /// <summary>
    /// Gets the underlying type from an expression by examining its body.
    /// </summary>
    private static Type GetUnderlyingTypeFromExpression(Expression expression)
    {
        if (expression is LambdaExpression lambda)
        {
            return GetUnderlyingTypeFromExpression(lambda.Body);
        }

        if (expression is UnaryExpression unary)
        {
            // Handle Convert expressions - get the operand type
            if (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked)
            {
                return GetUnderlyingTypeFromExpression(unary.Operand);
            }

            return GetUnderlyingTypeFromExpression(unary.Operand);
        }

        if (expression is MemberExpression member)
        {
            var memberType = member.Member is PropertyInfo prop ? prop.PropertyType : ((FieldInfo)member.Member).FieldType;
            return Nullable.GetUnderlyingType(memberType) ?? memberType;
        }

        if (expression is MethodCallExpression methodCall)
        {
            return methodCall.Method.ReturnType;
        }

        return expression.Type;
    }

    /// <summary>
    /// Fails when a column takes its time zone from a property that is not a column: only columns are
    /// projected, so its rows would carry no zone.
    /// </summary>
    private static void EnsureTimeZoneColumnsExist(List<TableViewColumnMetadata> metadata)
    {
        foreach (var column in metadata)
        {
            if (column.TimeZoneColumn is { } zoneColumn && !metadata.Exists(c => c.Name == zoneColumn))
            {
                throw new InvalidOperationException(
                    $"Column '{column.Name}' takes its time zone from '{zoneColumn}', which is not a column of the " +
                    $"table view. Configure it with Column(dto => dto.{zoneColumn}) so every row carries its zone.");
            }
        }
    }

    private static IEnumerable<TableViewAllowedValue> CreateEnumAllowedValues(Type enumType)
    {
        return Enum.GetNames(enumType).Select(memberName =>
            new TableViewAllowedValue(memberName, TableViewLabelHumanizer.Humanize(memberName))
            {
                LabelKey = $"Enums.{enumType.Name}.{memberName}",
                IsLabelKeyOptional = true,
            });
    }

    private static string GetPropertyName(LambdaExpression expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpr })
        {
            return unaryMemberExpr.Member.Name;
        }

        throw new ArgumentException("Expression must be a property accessor", nameof(expression));
    }

    private static PropertyInfo GetProperty(LambdaExpression expression)
    {
        if (expression.Body is MemberExpression memberExpr && memberExpr.Member is PropertyInfo prop1)
        {
            return prop1;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpr } && unaryMemberExpr.Member is PropertyInfo prop2)
        {
            return prop2;
        }

        throw new ArgumentException("Expression must be a property accessor", nameof(expression));
    }

    private static string GetDataType(LambdaExpression expression)
    {
        var property = GetProperty(expression);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return TableViewDataTypes.String;
        }

        if (type == typeof(bool))
        {
            return TableViewDataTypes.Boolean;
        }

        if (type == typeof(DateOnly))
        {
            return TableViewDataTypes.Date;
        }

        if (type == typeof(DateTime))
        {
            return TableViewDataTypes.LocalDateTime;
        }

        if (type == typeof(DateTimeOffset))
        {
            return TableViewDataTypes.Instant;
        }

        if (type.IsEnum)
        {
            return TableViewDataTypes.Enum;
        }

        if (IsNumericType(type))
        {
            return TableViewDataTypes.Number;
        }

        if (type == typeof(Guid))
        {
            return TableViewDataTypes.Guid;
        }

        return TableViewDataTypes.String;
    }

    private static LambdaExpression? CreateEntityExpression(string propertyName)
    {
        var property = typeof(TEntity).GetProperty(propertyName);
        if (property == null)
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Property(parameter, property);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(TEntity), property.PropertyType);
        return Expression.Lambda(delegateType, propertyAccess, parameter);
    }

    private static Expression ReplaceLambdaParameter(LambdaExpression expression, ParameterExpression replacementParameter)
    {
        var originalParameter = expression.Parameters.Single();
        var visitor = new ParameterReplaceVisitor(originalParameter, replacementParameter);
        return visitor.Visit(expression.Body)
            ?? throw new InvalidOperationException("Failed to rewrite lambda expression body.");
    }

    private static Expression? BuildEqualsExpression(Expression propertyAccess, string filterValue, string dataType, TimeZoneInfo timeZone)
    {
        object? value = null;
        var parsed = false;

        if (dataType == TableViewDataTypes.String)
        {
            (value, parsed) = (filterValue, true);
        }
        else if (dataType == TableViewDataTypes.Boolean)
        {
            parsed = bool.TryParse(filterValue, out var boolValue);
            value = boolValue;
        }
        else if (dataType == TableViewDataTypes.Number)
        {
            parsed = TryParseNumeric(filterValue, propertyAccess.Type, out value);
        }
        else if (dataType == TableViewDataTypes.Date || dataType == TableViewDataTypes.LocalDateTime || dataType == TableViewDataTypes.Instant)
        {
            parsed = TableViewTemporalFilterValues.TryParseValue(filterValue, propertyAccess.Type, timeZone, out value, out var namesNoInstant);

            // A reading in a spring-forward gap is well-formed but no instant equals it: match nothing,
            // rather than dropping the filter and matching every row.
            if (namesNoInstant)
            {
                return Expression.Constant(false);
            }
        }
        else if (dataType == TableViewDataTypes.Guid)
        {
            parsed = Guid.TryParse(filterValue, CultureInfo.InvariantCulture, out var guidValue);
            value = guidValue;
        }
        else
        {
            // Other data types support no equality filter, so the value is left unparsed.
        }

        return parsed ? Expression.Equal(propertyAccess, Expression.Constant(value, propertyAccess.Type)) : null;
    }

    private static BinaryExpression? BuildRangeExpression(Expression propertyAccess, string filterValue, string dataType)
    {
        if (dataType != TableViewDataTypes.Number)
        {
            return null;
        }

        // The bound separator is the first '-' after position 0 that does not directly follow
        // another '-'. A leading '-' therefore stays with the minimum bound ("-5-10" is -5..10),
        // and when the leading '-' is the only candidate it acts as the separator of the
        // open-minimum form ("-20" is ..20). A '-' right after the separator belongs to a
        // negative maximum ("-5--1" is -5..-1).
        var separatorIndex = -1;
        for (var i = 1; i < filterValue.Length; i++)
        {
            if (filterValue[i] == '-' && filterValue[i - 1] != '-')
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 0)
        {
            if (filterValue.Length == 0 || filterValue[0] != '-')
            {
                return null;
            }

            separatorIndex = 0;
        }

        var minPart = filterValue[..separatorIndex];
        var maxPart = filterValue[(separatorIndex + 1)..];

        object? minValue = null;
        object? maxValue = null;
        var hasMin = !string.IsNullOrWhiteSpace(minPart) && TryParseNumeric(minPart, propertyAccess.Type, out minValue);
        var hasMax = !string.IsNullOrWhiteSpace(maxPart) && TryParseNumeric(maxPart, propertyAccess.Type, out maxValue);

        if (hasMin && hasMax)
        {
            var minExpr = Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(minValue, propertyAccess.Type));
            var maxExpr = Expression.LessThanOrEqual(propertyAccess, Expression.Constant(maxValue, propertyAccess.Type));
            return Expression.AndAlso(minExpr, maxExpr);
        }

        if (hasMin)
        {
            return Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(minValue, propertyAccess.Type));
        }

        if (hasMax)
        {
            return Expression.LessThanOrEqual(propertyAccess, Expression.Constant(maxValue, propertyAccess.Type));
        }

        return null;
    }

    private static Expression? BuildInExpression(Expression propertyAccess, string filterValue, string dataType, TimeZoneInfo timeZone)
    {
        var values = filterValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
        {
            return null;
        }

        Expression? result = null;
        foreach (var value in values)
        {
            var equalsExpr = BuildEqualsExpression(propertyAccess, value, dataType, timeZone);
            if (equalsExpr != null)
            {
                result = result == null ? equalsExpr : Expression.OrElse(result, equalsExpr);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a date range filter from <c>from,to</c> calendar dates, either side optional, with the
    /// semantics of the column's kind (see <see cref="TableViewTemporalFilterValues.TryGetDayBounds"/>).
    /// </summary>
    private static Expression? BuildDateRangeExpression(Expression propertyAccess, string filterValue, TimeZoneInfo timeZone)
    {
        var parts = filterValue.Split(',', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        DateOnly? from = TableViewTemporalFilterValues.TryParseDate(parts[0], out var fromDate) ? fromDate : null;
        DateOnly? to = TableViewTemporalFilterValues.TryParseDate(parts[1], out var toDate) ? toDate : null;
        if ((from is null && to is null)
            || !TableViewTemporalFilterValues.TryGetDayBounds(propertyAccess.Type, from, to, timeZone, out var bounds))
        {
            return null;
        }

        var lowerExpr = bounds.Lower is null
            ? null
            : Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(bounds.Lower, propertyAccess.Type));

        Expression? upperExpr = null;
        if (bounds.Upper is not null)
        {
            var upperConstant = Expression.Constant(bounds.Upper, propertyAccess.Type);
            upperExpr = bounds.IsUpperInclusive
                ? Expression.LessThanOrEqual(propertyAccess, upperConstant)
                : Expression.LessThan(propertyAccess, upperConstant);
        }

        if (lowerExpr is not null && upperExpr is not null)
        {
            return Expression.AndAlso(lowerExpr, upperExpr);
        }

        return lowerExpr ?? upperExpr;
    }

    private static BinaryExpression? BuildEnumExpression(Expression propertyAccess, string filterValue)
    {
        var enumType = Nullable.GetUnderlyingType(propertyAccess.Type) ?? propertyAccess.Type;
        if (!enumType.IsEnum)
        {
            return null;
        }

        // Enum.TryParse accepts any numeric string, defined or not, so definedness is checked
        // explicitly to reject undefined values like any other malformed filter input.
        if (Enum.TryParse(enumType, filterValue, true, out var enumValue) && Enum.IsDefined(enumType, enumValue!))
        {
            return Expression.Equal(propertyAccess, Expression.Constant(enumValue, propertyAccess.Type));
        }

        return null;
    }

    private static bool TryParseNumeric(string value, Type targetType, out object? result)
    {
        result = null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (type == typeof(int))
            {
                result = int.Parse(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (type == typeof(decimal))
            {
                result = decimal.Parse(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (type == typeof(double))
            {
                result = double.Parse(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (type == typeof(float))
            {
                result = float.Parse(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (type == typeof(long))
            {
                result = long.Parse(value, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified type is a numeric type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is numeric; otherwise, false.</returns>
    private static bool IsNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Use a switch expression to check against all numeric types
        return underlyingType switch
        {
            Type t when t == typeof(int) => true,
            Type t when t == typeof(decimal) => true,
            Type t when t == typeof(double) => true,
            Type t when t == typeof(float) => true,
            Type t when t == typeof(long) => true,
            Type t when t == typeof(short) => true,
            Type t when t == typeof(byte) => true,
            Type t when t == typeof(uint) => true,
            Type t when t == typeof(ulong) => true,
            Type t when t == typeof(ushort) => true,
            _ => false,
        };
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : base.VisitParameter(node);
        }
    }
}
