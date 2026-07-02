namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Default implementation of <see cref="ITableViewCatalogRegistration"/> used by the blueprint infrastructure.
/// Provides catalog metadata for a table view definition that implements <see cref="ITableViewDefinitionInfo"/>.
/// </summary>
public sealed class TableViewCatalogRegistration : ITableViewCatalogRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewCatalogRegistration"/> class.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    /// <param name="requiredPermission">The required permission.</param>
    /// <param name="entityType">The entity type name.</param>
    public TableViewCatalogRegistration(
        string viewId,
        string displayName,
        string description,
        string requiredPermission,
        string entityType)
    {
        this.ViewId = viewId;
        this.DisplayName = displayName;
        this.Description = description;
        this.RequiredPermission = requiredPermission;
        this.EntityType = entityType;
    }

    /// <inheritdoc/>
    public string ViewId { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public string RequiredPermission { get; }

    /// <inheritdoc/>
    public string EntityType { get; }
}
