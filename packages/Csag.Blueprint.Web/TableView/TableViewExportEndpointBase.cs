namespace Csag.Blueprint.Web.TableView;

using System.Globalization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.TableView;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Generic base endpoint for exporting table view data as an Excel file.
/// Derived endpoints only need to override <see cref="BuildQuery"/> and Configure for routing/permissions.
/// The request's <see cref="TableViewExportRequest.TimeZone"/>, else the zone of the registered
/// <see cref="ITenantTimeZoneProvider"/>, decides both how date filters on instant columns are evaluated and
/// in which zone the workbook shows instants; dates and times are formatted for the request culture. The
/// headers of instant columns name that zone, and the headers of local reading columns name the tenant's, unless
/// their zone comes from the row.
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

    /// <summary>
    /// Gets the largest number of rows an export without <see cref="TableViewExportRequest.Page"/> and
    /// <see cref="TableViewExportRequest.PageSize"/> may hold. Such an export is refused with
    /// <c>400 Bad Request</c>, not truncated, when more rows match its filters. An export of one page is
    /// bounded by <see cref="TableViewExportRequestValidator"/> instead.
    /// Override to customize per entity.
    /// </summary>
    protected virtual int MaxExportRows => 10_000;

    /// <summary>
    /// Gets the column metadata with request-time localization applied for the current request culture.
    /// Use this instead of <c>ViewDefinition.Metadata</c> when building responses in overriding endpoints,
    /// so translated column headers stay consistent across all table view endpoints. Each access re-runs
    /// localization — read it once per request and reuse the result.
    /// </summary>
    protected IList<TableViewColumnMetadata> LocalizedMetadata =>
        this.Resolve<ITableViewMetadataLocalizer>().Localize(this.definition.Metadata);

    /// <inheritdoc/>
    public override async Task HandleAsync(TableViewExportRequest req, CancellationToken ct)
    {
        if (!TableViewRequestTimeZone.TryFindRequested(req.TimeZone, out var requestedTimeZone))
        {
            this.ThrowError(r => r.TimeZone, TableViewRequestTimeZone.InvalidMessage);
        }

        var tenantTimeZone = await this.Resolve<ITenantTimeZoneProvider>().GetTimeZoneAsync(ct);
        var timeZone = requestedTimeZone ?? tenantTimeZone;

        // The validator requires Page and PageSize together. Without them the export reads one page of
        // MaxExportRows rows plus one, which bounds the query. The count and the page are separate reads, so
        // a row matching between them can be missing from the count; the extra row still reveals it.
        var isPageExport = req.Page.HasValue && req.PageSize.HasValue;
        var maxExportRows = this.MaxExportRows;

        var request = new TableViewQueryRequest<TEntity, TDto>
        {
            Query = this.BuildQuery(),
            Definition = this.definition,
            Filters = req.Filters,
            SortColumns = req.SortColumns,
            TimeZone = timeZone,
            Page = req.Page ?? 1,
            PageSize = req.PageSize ?? (maxExportRows == int.MaxValue ? maxExportRows : maxExportRows + 1),
        };

        var (data, totalCount) = await this.tableViewExecutor.ExecuteAsync(request, ct);

        if (!isPageExport && (totalCount > maxExportRows || data.Count > maxExportRows))
        {
            this.ThrowError(
                $"The export matches more than the {maxExportRows} rows an export may hold. Narrow the filters or export one page.");
        }

        var excelBytes = TableViewExcelExporter.Export(
            data,
            this.LocalizedMetadata,
            this.ExportFileName,
            timeZone: timeZone,
            placeTimeZone: tenantTimeZone,
            culture: CultureInfo.CurrentCulture);

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
