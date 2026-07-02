namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Provides static metadata about a table view definition.
/// Implementations expose view identity (ViewId, DisplayName, Description) and the required permission
/// so that generic infrastructure can discover and configure endpoints without entity-specific code.
/// </summary>
public interface ITableViewDefinitionInfo
{
    /// <summary>
    /// Gets the unique identifier for the table view.
    /// </summary>
    static abstract string ViewId { get; }

    /// <summary>
    /// Gets the display name for the table view.
    /// </summary>
    static abstract string DisplayName { get; }

    /// <summary>
    /// Gets the description of the table view.
    /// </summary>
    static abstract string Description { get; }

    /// <summary>
    /// Gets the required permission to read this table view.
    /// </summary>
    static abstract string RequiredPermission { get; }
}
