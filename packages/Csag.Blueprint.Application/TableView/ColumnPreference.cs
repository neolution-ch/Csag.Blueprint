namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Represents column layout preferences for a table view.
/// </summary>
public sealed class ColumnPreference
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the column is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the display order of the column (0-based index).
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the column width in pixels (optional).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is pinned (frozen).
    /// </summary>
    public bool IsPinned { get; set; }
}
