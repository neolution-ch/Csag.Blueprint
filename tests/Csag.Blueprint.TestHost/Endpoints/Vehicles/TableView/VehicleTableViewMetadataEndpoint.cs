namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;

using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;

/// <summary>
/// Serves the vehicles table view column metadata.
/// </summary>
public sealed class VehicleTableViewMetadataEndpoint
    : TableViewMetadataEndpointBase<TestVehicle, VehicleTableViewDto, VehicleTableViewDefinition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleTableViewMetadataEndpoint"/> class.
    /// </summary>
    /// <param name="definition">The vehicles table view definition.</param>
    public VehicleTableViewMetadataEndpoint(VehicleTableViewDefinition definition)
        : base(definition)
    {
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/table-view/metadata");
        this.Policies(PolicyNames.CanReadVehicles);
        this.Summary(s =>
        {
            s.Summary = "Retrieve vehicles table view metadata";
            s.Description = "Describes the available columns, their types, and capabilities (filterable, sortable).";
        });
    }
}
