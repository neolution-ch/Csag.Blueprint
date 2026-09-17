namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Serves the vehicles table view data with dynamic filtering, sorting, and pagination.
/// </summary>
public sealed class VehicleTableViewDataEndpoint
    : TableViewDataEndpointBase<TestDbContext, TestVehicle, VehicleTableViewDto, VehicleTableViewDefinition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTableViewDataEndpoint"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="tableViewExecutor">The table view query executor.</param>
    /// <param name="definition">The vehicles table view definition.</param>
    public VehicleTableViewDataEndpoint(
        TestDbContext context,
        ITableViewExecutor tableViewExecutor,
        VehicleTableViewDefinition definition)
        : base(context, tableViewExecutor, definition)
    {
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/table-view/data");
        this.Policies(PolicyNames.CanReadVehicles);
        this.Summary(s =>
        {
            s.Summary = "Retrieve vehicles table view data";
            s.Description = "Retrieves vehicle rows with dynamic filtering, sorting, and pagination, plus column metadata.";
        });
    }

    /// <inheritdoc/>
    protected override IQueryable<TestVehicle> BuildQuery()
    {
        return this.Context.Vehicles.AsNoTracking();
    }
}
