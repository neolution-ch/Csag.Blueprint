namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Generic base endpoint for exporting table view data as an Excel file.
/// Derived endpoints only need to override <see cref="BuildQuery"/> and Configure for routing/permissions.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type for projection.</typeparam>
/// <typeparam name="TDefinition">The concrete table view definition type.</typeparam>
#pragma warning disable S2436 // Type parameters are intrinsic to this generic base pattern
public abstract class TableViewExportEndpointBase<TContext, TEntity, TDto, TDefinition>
#pragma warning restore S2436
    : Endpoint<TableViewExportRequest>
    where TContext : DbContext
    where TEntity : class
    where TDto : class, new()
    where TDefinition : class, ITableViewDefinition<TEntity, TDto>
{
    private readonly TContext context;
    private readonly ITableViewExecutor tableViewExecutor;
    private readonly TDefinition definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewExportEndpointBase{TContext, TEntity, TDto, TDefinition}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="tableViewExecutor">The table view query executor.</param>
    /// <param name="definition">The table view definition.</param>
    protected TableViewExportEndpointBase(
        TContext context,
        ITableViewExecutor tableViewExecutor,
        TDefinition definition)
    {
        this.context = context;
        this.tableViewExecutor = tableViewExecutor;
        this.definition = definition;
    }

    /// <summary>
    /// Gets the database context for building queries.
    /// </summary>
    protected TContext Context => this.context;

    /// <summary>
    /// Gets the table view definition.
    /// </summary>
    protected TDefinition ViewDefinition => this.definition;

    /// <summary>
    /// Gets the file name for the exported Excel file (without extension).
    /// Override to customize per entity.
    /// </summary>
    protected virtual string ExportFileName => "export";

    /// <inheritdoc/>
    public override async Task HandleAsync(TableViewExportRequest req, CancellationToken ct)
    {
        var query = this.BuildQuery();

        IList<TDto> data;

        if (req.Page.HasValue && req.PageSize.HasValue)
        {
            var request = new TableViewQueryRequest<TEntity, TDto>
            {
                Query = query,
                Definition = this.definition,
                Filters = req.Filters,
                SortColumns = req.SortColumns,
                Page = req.Page.Value,
                PageSize = req.PageSize.Value,
            };

            (data, _) = await this.tableViewExecutor.ExecuteAsync(request, ct);
        }
        else
        {
            data = await this.tableViewExecutor.ExecuteWithoutPaginationAsync(
                query,
                this.definition,
                req.Filters,
                req.SortColumns,
                ct);
        }

        var excelBytes = TableViewExcelExporter.Export(data, this.definition.Metadata, this.ExportFileName);

        await this.Send.BytesAsync(
            excelBytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName: $"{this.ExportFileName}.xlsx",
            cancellation: ct);
    }

    /// <summary>
    /// Builds the base <see cref="IQueryable{TEntity}"/> for the export, including any necessary
    /// navigation property includes. The query should use <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>.
    /// </summary>
    /// <returns>The base queryable for the entity.</returns>
    protected abstract IQueryable<TEntity> BuildQuery();
}
