namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service implementation for the table view catalog.
/// Provides discovery capabilities for available table views.
/// </summary>
public sealed class TableViewCatalogService : ITableViewCatalogService
{
    private readonly IEnumerable<ITableViewCatalogRegistration> registrations;
    private readonly ILogger<TableViewCatalogService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewCatalogService"/> class.
    /// </summary>
    /// <param name="registrations">The collection of registered table view catalog entries.</param>
    /// <param name="logger">The logger.</param>
    public TableViewCatalogService(
        IEnumerable<ITableViewCatalogRegistration> registrations,
        ILogger<TableViewCatalogService> logger)
    {
        this.registrations = registrations;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<IList<TableViewCatalogItem>> GetAvailableViewsAsync(
        IEnumerable<string> userPermissions,
        CancellationToken cancellationToken = default)
    {
        var userPermissionSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);

        var availableViews = this.registrations
            .Where(r => userPermissionSet.Contains(r.RequiredPermission))
            .Select(r => new TableViewCatalogItem
            {
                ViewId = r.ViewId,
                DisplayName = r.DisplayName,
                Description = r.Description,
                RequiredPermission = r.RequiredPermission,
                EntityType = r.EntityType,
            })
            .ToList();

        this.logger.LogDebug(
            "Retrieved {Count} available table views for user with {PermissionCount} permissions",
            availableViews.Count,
            userPermissionSet.Count);

        return Task.FromResult<IList<TableViewCatalogItem>>(availableViews);
    }

    /// <inheritdoc/>
    public Task<TableViewCatalogItem?> GetViewByIdAsync(
        string viewId,
        CancellationToken cancellationToken = default)
    {
        var registration = this.registrations
            .FirstOrDefault(r => r.ViewId.Equals(viewId, StringComparison.OrdinalIgnoreCase));

        if (registration == null)
        {
            this.logger.LogWarning("Table view with ID '{ViewId}' not found", viewId);
            return Task.FromResult<TableViewCatalogItem?>(null);
        }

        var item = new TableViewCatalogItem
        {
            ViewId = registration.ViewId,
            DisplayName = registration.DisplayName,
            Description = registration.Description,
            RequiredPermission = registration.RequiredPermission,
            EntityType = registration.EntityType,
        };

        return Task.FromResult<TableViewCatalogItem?>(item);
    }
}
