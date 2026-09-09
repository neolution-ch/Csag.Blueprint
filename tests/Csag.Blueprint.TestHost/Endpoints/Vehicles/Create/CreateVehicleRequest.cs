namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;

using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Payload for creating a vehicle in the caller's active tenant.
/// </summary>
public sealed class CreateVehicleRequest
{
    /// <summary>
    /// Gets or sets the vehicle name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind of vehicle.
    /// </summary>
    public TestVehicleKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the maximum capacity (number of people).
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Gets or sets the hourly rental price.
    /// </summary>
    public decimal PricePerHour { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the vehicle is active. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the date the vehicle was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }
}
