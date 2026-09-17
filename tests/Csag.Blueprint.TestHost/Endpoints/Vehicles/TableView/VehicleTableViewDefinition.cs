namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Table view definition for <see cref="TestVehicle"/>. The columns cover every filter operator
/// the executor supports — equals, string contains, enum, numeric range, boolean, and date range —
/// so table-view behaviour can be asserted end to end against real seeded data.
/// </summary>
public sealed class VehicleTableViewDefinition : TableViewDefinition<TestVehicle, VehicleTableViewDto>, ITableViewDefinitionInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTableViewDefinition"/> class.
    /// </summary>
    public VehicleTableViewDefinition()
    {
        this.Column(dto => dto.Id, e => e.Id)
            .Filterable(TableViewFilterOperator.Equals)
            .Sortable()
            .WithDescription("Unique identifier");

        this.Column(dto => dto.Name, e => e.Name)
            .Filterable(TableViewFilterOperator.Contains)
            .Sortable()
            .WithDescription("Vehicle name");

        this.Column(dto => dto.Kind, e => e.Kind)
            .Filterable(TableViewFilterOperator.Enum)
            .Sortable()
            .WithDescription("Vehicle kind");

        this.Column(dto => dto.Capacity, e => e.Capacity)
            .Filterable(TableViewFilterOperator.Range)
            .Sortable()
            .WithDescription("Seating capacity");

        this.Column(dto => dto.PricePerHour, e => e.PricePerHour)
            .Filterable(TableViewFilterOperator.Range)
            .Sortable()
            .WithDescription("Hourly rental price");

        this.Column(dto => dto.IsActive, e => e.IsActive)
            .Filterable(TableViewFilterOperator.Boolean)
            .Sortable()
            .WithDescription("Whether the vehicle is active");

        this.Column(dto => dto.AcquiredAt, e => e.AcquiredAt)
            .Filterable(TableViewFilterOperator.DateRange)
            .Sortable()
            .WithDescription("Acquisition date");

        this.Column(dto => dto.CreatedAt, e => e.CreatedAt)
            .Filterable(TableViewFilterOperator.DateRange)
            .Sortable()
            .WithDescription("Creation date");
    }

    /// <summary>
    /// Gets the unique identifier of the vehicles table view.
    /// </summary>
    public static string ViewId => "vehicles";

    /// <summary>
    /// Gets the display name of the vehicles table view.
    /// </summary>
    public static string DisplayName => "Vehicles";

    /// <summary>
    /// Gets the description of the vehicles table view.
    /// </summary>
    public static string Description => "Table view over the tenant's vehicles";

    /// <summary>
    /// Gets the permission required to read this table view.
    /// </summary>
    public static string RequiredPermission => TestPermissions.VehiclesRead;
}
