namespace Csag.Blueprint.TestHost.Endpoints.MaintenanceRecords.List;

using FastEndpoints;

/// <summary>
/// Returns a fixed set of maintenance records to any authenticated caller. The endpoint's purpose
/// is its route: the multi-word namespace segment resolves to a kebab-case path
/// (<c>/api/maintenance-records</c>), pinning the routing convention.
/// </summary>
public sealed class ListMaintenanceRecordsEndpoint : EndpointWithoutRequest<List<MaintenanceRecordResponse>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]");
        this.Summary(s =>
        {
            s.Summary = "List maintenance records";
            s.Description = "Returns a static list; requires authentication but no specific permission.";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        List<MaintenanceRecordResponse> records =
        [
            new MaintenanceRecordResponse
            {
                VehicleName = "City Bike",
                Description = "Brake pads replaced",
                PerformedAt = new DateOnly(2025, 2, 10),
            },
            new MaintenanceRecordResponse
            {
                VehicleName = "Lake Kayak",
                Description = "Hull resealed",
                PerformedAt = new DateOnly(2025, 4, 2),
            },
        ];

        await this.Send.OkAsync(records, ct);
    }
}
