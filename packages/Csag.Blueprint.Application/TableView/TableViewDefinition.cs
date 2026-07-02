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

    /// <inheritdoc/>
    public Expression<Func<TEntity, bool>>? GetFilterExpression(string columnName, string filterValue)
    {
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
                filterExpression = BuildEqualsExpression(typedAccess, filterValue, config.Metadata.DataType);
                break;

            case TableViewFilterOperator.Contains:
                if (config.Metadata.DataType == "string")
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
                filterExpression = BuildInExpression(typedAccess, filterValue, config.Metadata.DataType);
                break;

            case TableViewFilterOperator.DateRange:
                filterExpression = BuildDateRangeExpression(typedAccess, filterValue);
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

        if (dataType == "enum")
        {
            var property = GetProperty(dtoPropertySelector);
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            config.WithAllowedValues(Enum.GetNames(enumType));
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

        const string stringType = "string";
        const string numberType = "number";
        const string booleanType = "boolean";
        const string dateType = "date";
        const string enumType = "enum";

        if (type == typeof(string))
        {
            return stringType;
        }

        if (type == typeof(bool))
        {
            return booleanType;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
        {
            return dateType;
        }

        if (type.IsEnum)
        {
            return enumType;
        }

        if (IsNumericType(type))
        {
            return numberType;
        }

        if (type == typeof(Guid))
        {
            return "guid";
        }

        return stringType;
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

    private static BinaryExpression? BuildEqualsExpression(Expression propertyAccess, string filterValue, string dataType)
    {
        return dataType switch
        {
            "string" => Expression.Equal(propertyAccess, Expression.Constant(filterValue)),
            "number" => TryParseNumeric(filterValue, propertyAccess.Type, out var numValue)
                ? Expression.Equal(propertyAccess, Expression.Constant(numValue, propertyAccess.Type))
                : null,
            "date" => TryParseDate(filterValue, propertyAccess.Type, out var dateValue)
                ? Expression.Equal(propertyAccess, Expression.Constant(dateValue, propertyAccess.Type))
                : null,
            "guid" => Guid.TryParse(filterValue, CultureInfo.InvariantCulture, out var guidValue)
                ? Expression.Equal(propertyAccess, Expression.Constant(guidValue, propertyAccess.Type))
                : null,
            _ => null,
        };
    }

    private static BinaryExpression? BuildRangeExpression(Expression propertyAccess, string filterValue, string dataType)
    {
        var parts = filterValue.Split('-', 2);
        if (parts.Length != 2 || dataType != "number")
        {
            return null;
        }

        object? minValue = null;
        object? maxValue = null;
        var hasMin = !string.IsNullOrWhiteSpace(parts[0]) && TryParseNumeric(parts[0], propertyAccess.Type, out minValue);
        var hasMax = !string.IsNullOrWhiteSpace(parts[1]) && TryParseNumeric(parts[1], propertyAccess.Type, out maxValue);

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

    private static Expression? BuildInExpression(Expression propertyAccess, string filterValue, string dataType)
    {
        var values = filterValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
        {
            return null;
        }

        Expression? result = null;
        foreach (var value in values)
        {
            var equalsExpr = BuildEqualsExpression(propertyAccess, value, dataType);
            if (equalsExpr != null)
            {
                result = result == null ? equalsExpr : Expression.OrElse(result, equalsExpr);
            }
        }

        return result;
    }

    private static BinaryExpression? BuildDateRangeExpression(Expression propertyAccess, string filterValue)
    {
        var parts = filterValue.Split(',', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        object? startDate = null;
        object? endDate = null;
        var hasStart = !string.IsNullOrWhiteSpace(parts[0]) && TryParseDate(parts[0], propertyAccess.Type, out startDate);
        var hasEnd = !string.IsNullOrWhiteSpace(parts[1]) && TryParseDate(parts[1], propertyAccess.Type, out endDate);

        if (hasStart && hasEnd)
        {
            var startExpr = Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(startDate, propertyAccess.Type));
            var endExpr = Expression.LessThanOrEqual(propertyAccess, Expression.Constant(endDate, propertyAccess.Type));
            return Expression.AndAlso(startExpr, endExpr);
        }

        if (hasStart)
        {
            return Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(startDate, propertyAccess.Type));
        }

        if (hasEnd)
        {
            return Expression.LessThanOrEqual(propertyAccess, Expression.Constant(endDate, propertyAccess.Type));
        }

        return null;
    }

    private static BinaryExpression? BuildEnumExpression(Expression propertyAccess, string filterValue)
    {
        var enumType = Nullable.GetUnderlyingType(propertyAccess.Type) ?? propertyAccess.Type;
        if (!enumType.IsEnum)
        {
            return null;
        }

        if (Enum.TryParse(enumType, filterValue, true, out var enumValue))
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

    /// <summary>
    /// Tries to parse a date string into the appropriate date type (<see cref="DateOnly"/>, <see cref="DateTimeOffset"/>, or <see cref="DateTime"/>).
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="targetType">The target property type.</param>
    /// <param name="result">The parsed value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    private static bool TryParseDate(string value, Type targetType, out object? result)
    {
        result = null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (type == typeof(DateOnly) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var dateOnly))
        {
            result = dateOnly;
            return true;
        }

        if (type == typeof(DateTimeOffset) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, out var dto))
        {
            result = dto;
            return true;
        }

        if (type == typeof(DateTime) && DateTime.TryParse(value, CultureInfo.InvariantCulture, out var dt))
        {
            result = dt;
            return true;
        }

        return false;
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : base.VisitParameter(node);
        }
    }
}
