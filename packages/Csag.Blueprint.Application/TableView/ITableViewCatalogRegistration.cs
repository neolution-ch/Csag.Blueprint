namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Defines a contract for registering a table view in the catalog.
/// Implementations are automatically discovered and registered in DI.
/// </summary>
public interface ITableViewCatalogRegistration
{
    /// <summary>
    /// Gets the unique identifier for the table view.
    /// </summary>
    string ViewId { get; }

    /// <summary>
    /// Gets the display name for the table view.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the description of the table view.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the required permission to access this table view.
    /// </summary>
    string RequiredPermission { get; }

    /// <summary>
    /// Gets the entity type name this table view represents.
    /// </summary>
    string EntityType { get; }
}
