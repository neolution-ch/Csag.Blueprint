namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Defines a contract for the table view catalog service.
/// Provides discovery capabilities for available table views.
/// </summary>
public interface ITableViewCatalogService
{
    /// <summary>
    /// Gets all available table views that the current user has permission to access.
    /// </summary>
    /// <param name="userPermissions">The permissions assigned to the current user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of table view catalog items.</returns>
    Task<IList<TableViewCatalogItem>> GetAvailableViewsAsync(
        IEnumerable<string> userPermissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific table view catalog item by its ID.
    /// </summary>
    /// <param name="viewId">The table view identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalog item if found; otherwise, null.</returns>
    Task<TableViewCatalogItem?> GetViewByIdAsync(
        string viewId,
        CancellationToken cancellationToken = default);
}
