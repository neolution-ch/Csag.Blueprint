namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;

using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Projection DTO for the vehicles table view.
/// </summary>
public sealed class VehicleTableViewDto
{
    /// <summary>
    /// Gets or sets the vehicle identifier.
    /// </summary>
    public Guid Id { get; set; }

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
    /// Gets or sets a value indicating whether the vehicle is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the date the vehicle was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp the row was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
