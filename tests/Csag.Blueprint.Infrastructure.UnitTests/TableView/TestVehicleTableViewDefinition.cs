namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Table view definition for <see cref="TestVehicle"/>. Its columns cover the filter operators the
/// executor supports: equals, string contains, enum, numeric range, boolean, and date range.
/// </summary>
public sealed class TestVehicleTableViewDefinition : TableViewDefinition<TestVehicle, TestVehicleTableViewDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestVehicleTableViewDefinition"/> class.
    /// </summary>
    public TestVehicleTableViewDefinition()
    {
        // Id - entity property
        this.Column(dto => dto.Id, e => e.Id)
            .Filterable(TableViewFilterOperator.Equals)
            .Sortable()
            .WithDescription("Unique identifier");

        // Name - entity property
        this.Column(dto => dto.Name, e => e.Name)
            .Filterable(TableViewFilterOperator.Contains)
            .Sortable()
            .WithDescription("Vehicle name");

        // Kind - enum property
        this.Column(dto => dto.Kind, e => e.Kind)
            .Filterable(TableViewFilterOperator.Enum)
            .Sortable()
            .WithDescription("Vehicle kind");

        // Capacity - entity property
        this.Column(dto => dto.Capacity, e => e.Capacity)
            .Filterable(TableViewFilterOperator.Range)
            .Sortable()
            .WithDescription("Seating capacity");

        // PricePerHour - entity property
        this.Column(dto => dto.PricePerHour, e => e.PricePerHour)
            .Filterable(TableViewFilterOperator.Range)
            .Sortable()
            .WithDescription("Hourly rental price");

        // IsActive - entity property
        this.Column(dto => dto.IsActive, e => e.IsActive)
            .Filterable(TableViewFilterOperator.Boolean)
            .Sortable()
            .WithDescription("Whether the vehicle is active");

        // AcquiredAt - entity property
        this.Column(dto => dto.AcquiredAt, e => e.AcquiredAt)
            .Filterable(TableViewFilterOperator.DateRange)
            .Sortable()
            .WithDescription("Acquisition date");

        // CreatedAt - entity property
        this.Column(dto => dto.CreatedAt, e => e.CreatedAt)
            .Filterable(TableViewFilterOperator.DateRange)
            .Sortable()
            .WithDescription("Creation date");
    }
}
