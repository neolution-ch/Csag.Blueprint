namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Lightweight summary of a saved table view preference, used for listing.
/// </summary>
public sealed class TableViewPreferenceSummary
{
    /// <summary>
    /// Gets or sets the unique identifier for the preference.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user-given name for this saved view.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the user's default view.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the preference was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
