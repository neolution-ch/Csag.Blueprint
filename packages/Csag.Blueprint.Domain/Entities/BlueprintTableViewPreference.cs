namespace Csag.Blueprint.Domain.Entities;

/// <summary>
/// Blueprint-owned canonical entity representing a user's saved preferences for a specific table view.
/// Generic on the concrete user type so applications retain a strongly typed user navigation.
/// A user can have multiple named preferences per table view, with one marked as default.
/// </summary>
/// <typeparam name="TUser">The concrete user type, must derive from <see cref="BlueprintUser"/>.</typeparam>
public class BlueprintTableViewPreference<TUser>
    where TUser : BlueprintUser
{
    /// <summary>
    /// Gets or sets the unique identifier for the preference.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID who owns this preference.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the table view identifier (for example, "pedalos").
    /// </summary>
    public string TableViewId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-given name for this saved view (e.g., "My favourites").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the user's default view for the table.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON payload containing the preferences.
    /// Includes column layout, filters, sorting, and page size.
    /// </summary>
    public string PreferencesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the preference was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the preference was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the concrete user who owns this preference.
    /// </summary>
    public TUser? User { get; set; }
}
