namespace Csag.Blueprint.TestHost.Endpoints.Vehicles;

using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// A vehicle as returned by the vehicle endpoints.
/// </summary>
public sealed class VehicleResponse
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
    /// Gets or sets the timestamp the row was created (stamped by the auditable-timestamp interceptor).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Maps a vehicle entity to its response shape.
    /// </summary>
    /// <param name="vehicle">The entity to map.</param>
    /// <returns>The mapped response.</returns>
    public static VehicleResponse FromEntity(TestVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return new VehicleResponse
        {
            Id = vehicle.Id,
            Name = vehicle.Name,
            Kind = vehicle.Kind,
            Capacity = vehicle.Capacity,
            PricePerHour = vehicle.PricePerHour,
            IsActive = vehicle.IsActive,
            AcquiredAt = vehicle.AcquiredAt,
            CreatedAt = vehicle.CreatedAt,
        };
    }
}
