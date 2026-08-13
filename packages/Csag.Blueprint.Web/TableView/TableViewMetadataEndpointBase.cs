namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using FastEndpoints;

/// <summary>
/// Generic base endpoint for table view metadata retrieval.
/// Derived endpoints only need to override the Configure method for routing and permissions.
/// All metadata is read from the <typeparamref name="TDefinition"/> which must implement <see cref="ITableViewDefinitionInfo"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type.</typeparam>
/// <typeparam name="TDefinition">The concrete definition type implementing both the definition interface and <see cref="ITableViewDefinitionInfo"/>.</typeparam>
#pragma warning disable S2436 // Three type parameters are intrinsic to this generic base pattern
public abstract class TableViewMetadataEndpointBase<TEntity, TDto, TDefinition>
#pragma warning restore S2436
    : EndpointWithoutRequest<TableViewMetadataResponse>
    where TEntity : class
    where TDto : class
    where TDefinition : class, ITableViewDefinition<TEntity, TDto>, ITableViewDefinitionInfo
{
    private readonly TDefinition definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewMetadataEndpointBase{TEntity, TDto, TDefinition}"/> class.
    /// </summary>
    /// <param name="definition">The table view definition.</param>
    protected TableViewMetadataEndpointBase(TDefinition definition)
    {
        this.definition = definition;
    }

    /// <summary>
    /// Gets the table view definition.
    /// </summary>
    protected TDefinition ViewDefinition => this.definition;

    /// <summary>
    /// Gets the column metadata with request-time localization applied for the current request culture.
    /// Use this instead of <c>ViewDefinition.Metadata</c> when building responses in overriding endpoints,
    /// so translated column headers stay consistent across all table view endpoints. Each access re-runs
    /// localization — read it once per request and reuse the result.
    /// </summary>
    protected IList<TableViewColumnMetadata> LocalizedMetadata =>
        this.Resolve<ITableViewMetadataLocalizer>().Localize(this.definition.Metadata);

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new TableViewMetadataResponse
        {
            ViewId = TDefinition.ViewId,
            DisplayName = TDefinition.DisplayName,
            Description = TDefinition.Description,
            Columns = this.LocalizedMetadata,
        };

        await this.Send.OkAsync(response, ct);
    }
}
