namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using FastEndpoints;

/// <summary>
/// Generic base endpoint for table view data queries with filtering, sorting, and pagination.
/// Derived endpoints only need to override <see cref="BuildQuery(TableViewDataRequest)"/> to provide the entity-specific
/// <see cref="IQueryable{TEntity}"/> and the Configure method for routing/permissions.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type for projection.</typeparam>
/// <typeparam name="TDefinition">The concrete table view definition type.</typeparam>
#pragma warning disable S2436 // Three type parameters are intrinsic to this generic base pattern
public abstract class TableViewDataEndpointBase<TEntity, TDto, TDefinition>
#pragma warning restore S2436
    : Endpoint<TableViewDataRequest, TableViewDataResponse<TDto>>
    where TEntity : class
    where TDto : class, new()
    where TDefinition : class, ITableViewDefinition<TEntity, TDto>
{
    private readonly ITableViewExecutor tableViewExecutor;
    private readonly TDefinition definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewDataEndpointBase{TEntity, TDto, TDefinition}"/> class.
    /// </summary>
    /// <param name="tableViewExecutor">The table view query executor.</param>
    /// <param name="definition">The table view definition.</param>
    protected TableViewDataEndpointBase(
        ITableViewExecutor tableViewExecutor,
        TDefinition definition)
    {
        this.tableViewExecutor = tableViewExecutor;
        this.definition = definition;
    }

    /// <summary>
    /// Gets the table view definition.
    /// </summary>
    protected TDefinition ViewDefinition => this.definition;

    /// <inheritdoc/>
    public override async Task HandleAsync(TableViewDataRequest req, CancellationToken ct)
    {
        var query = this.BuildQuery(req);

        var request = new TableViewQueryRequest<TEntity, TDto>
        {
            Query = query,
            Definition = this.definition,
            Filters = req.Filters,
            SortColumns = req.SortColumns,
            Page = req.Page,
            PageSize = req.PageSize,
        };

        var (data, totalCount) = await this.tableViewExecutor.ExecuteAsync(request, ct);

        var response = new TableViewDataResponse<TDto>
        {
            Metadata = this.definition.Metadata,
            Data = data,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize,
            TotalPages = req.PageSize > 0 ? (int)Math.Ceiling((double)totalCount / req.PageSize) : 0,
        };

        await this.Send.OkAsync(response, ct);
    }

    /// <summary>
    /// Builds the base <see cref="IQueryable{TEntity}"/> for the table view.
    /// </summary>
    /// <param name="request">The incoming table view data request.</param>
    /// <returns>The base queryable for the entity.</returns>
    protected abstract IQueryable<TEntity> BuildQuery(TableViewDataRequest request);
}

