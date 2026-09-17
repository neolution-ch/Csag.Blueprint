namespace Csag.Blueprint.TestHost.Endpoints.MaintenanceRecords.List;

/// <summary>
/// A maintenance record entry returned by the list endpoint.
/// </summary>
public sealed class MaintenanceRecordResponse
{
    /// <summary>
    /// Gets or sets the name of the maintained vehicle.
    /// </summary>
    public string VehicleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the work performed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the work was performed.
    /// </summary>
    public DateOnly PerformedAt { get; set; }
}
