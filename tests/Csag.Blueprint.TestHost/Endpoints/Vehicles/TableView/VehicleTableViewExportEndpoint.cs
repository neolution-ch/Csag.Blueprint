namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Exports the vehicles table view as an Excel file, honouring the same filters and sorting as the
/// data endpoint.
/// </summary>
public sealed class VehicleTableViewExportEndpoint
    : TableViewExportEndpointBase<TestDbContext, TestVehicle, VehicleTableViewDto, VehicleTableViewDefinition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTableViewExportEndpoint"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="tableViewExecutor">The table view query executor.</param>
    /// <param name="definition">The vehicles table view definition.</param>
    public VehicleTableViewExportEndpoint(
        TestDbContext context,
        ITableViewExecutor tableViewExecutor,
        VehicleTableViewDefinition definition)
        : base(context, tableViewExecutor, definition)
    {
    }

    /// <inheritdoc/>
    protected override string ExportFileName => "vehicles";

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/table-view/export");
        this.Policies(PolicyNames.CanReadVehicles);
        this.Description(d => d
            .Produces<byte[]>(200, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        this.Summary(s =>
        {
            s.Summary = "Export vehicles table view data as Excel";
        });
    }

    /// <inheritdoc/>
    protected override IQueryable<TestVehicle> BuildQuery()
    {
        return this.Context.Vehicles.AsNoTracking();
    }
}
