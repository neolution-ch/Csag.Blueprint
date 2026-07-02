namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Hints to the frontend about what kind of input control to render for a filter.
/// The backend auto-derives a default hint from the <see cref="TableViewFilterOperator"/>,
/// but it can be overridden for special cases (e.g., autocomplete for foreign key lookups).
/// </summary>
public enum TableViewFilterInputHint
{
    /// <summary>
    /// A plain text input field.
    /// </summary>
    Text,

    /// <summary>
    /// A dropdown select with predefined options (from <see cref="TableViewColumnMetadata.AllowedValues"/>).
    /// </summary>
    Select,

    /// <summary>
    /// Two numeric inputs for min/max range.
    /// </summary>
    NumberRange,

    /// <summary>
    /// A date range picker.
    /// </summary>
    DateRange,

    /// <summary>
    /// An async autocomplete/search input. The frontend must provide a lookup resolver for this column.
    /// </summary>
    Autocomplete,
}
