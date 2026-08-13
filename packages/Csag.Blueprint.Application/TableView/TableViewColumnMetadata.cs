namespace Csag.Blueprint.Application.TableView;

using System.Text.Json.Serialization;

/// <summary>
/// Describes metadata for a table view column.
/// </summary>
public sealed class TableViewColumnMetadata
{
    /// <summary>
    /// Gets or sets the name of the column.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the column for use as a human-readable header.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the translation key used to resolve <see cref="DisplayName"/> at request time.
    /// Recorded during definition construction without touching any localizer, so definitions stay
    /// safe to construct during application startup. Not serialized to clients; when the key cannot
    /// be resolved, <see cref="DisplayName"/> is served unchanged.
    /// </summary>
    [JsonIgnore]
    public string? DisplayNameKey { get; set; }

    /// <summary>
    /// Gets or sets the data type of the column (e.g., "string", "number", "boolean", "date", "enum").
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the column.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the translation key used to resolve <see cref="Description"/> at request time.
    /// Recorded during definition construction without touching any localizer, so definitions stay
    /// safe to construct during application startup. Not serialized to clients; when the key cannot
    /// be resolved, <see cref="Description"/> is served unchanged.
    /// </summary>
    [JsonIgnore]
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column can be filtered.
    /// </summary>
    public bool IsFilterable { get; set; }

    /// <summary>
    /// Gets or sets the type of filter that can be applied to this column.
    /// Only applicable if <see cref="IsFilterable"/> is true.
    /// </summary>
    public TableViewFilterOperator? FilterOperator { get; set; }

    /// <summary>
    /// Gets or sets the allowed values for enum or In-type filters.
    /// </summary>
    public IEnumerable<string>? AllowedValues { get; set; }

    /// <summary>
    /// Gets or sets the hint for the frontend about what kind of input control to render.
    /// Auto-derived from <see cref="FilterOperator"/> but can be overridden.
    /// </summary>
    public TableViewFilterInputHint? FilterInputHint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column can be sorted.
    /// </summary>
    public bool IsSortable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a computed column (not directly from the entity).
    /// </summary>
    public bool IsComputed { get; set; }

    /// <summary>
    /// Creates a shallow copy of this column metadata.
    /// Used by request-time localization to produce per-request localized metadata without
    /// mutating the instances owned by the (scoped) table view definition.
    /// </summary>
    /// <returns>A new <see cref="TableViewColumnMetadata"/> with the same property values.</returns>
    public TableViewColumnMetadata Clone() => (TableViewColumnMetadata)this.MemberwiseClone();
}
