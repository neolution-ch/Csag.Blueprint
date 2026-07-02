namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Defines the type of filter that can be applied to a column.
/// </summary>
public enum TableViewFilterOperator
{
    /// <summary>
    /// Exact match filter (e.g., column = value).
    /// </summary>
    Equals,

    /// <summary>
    /// Substring match filter (e.g., column LIKE %value%).
    /// </summary>
    Contains,

    /// <summary>
    /// Range filter for numeric or date values (e.g., column BETWEEN min AND max).
    /// </summary>
    Range,

    /// <summary>
    /// Match against a list of values (e.g., column IN (value1, value2, ...)).
    /// </summary>
    In,

    /// <summary>
    /// Boolean filter (true/false).
    /// </summary>
    Boolean,

    /// <summary>
    /// Date range filter (e.g., column BETWEEN startDate AND endDate).
    /// </summary>
    DateRange,

    /// <summary>
    /// Enum value filter with predefined allowed values.
    /// </summary>
    Enum,
}
