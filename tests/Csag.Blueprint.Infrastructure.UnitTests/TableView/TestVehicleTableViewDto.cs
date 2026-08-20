namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// DTO for <see cref="TestVehicle"/> table view results.
/// </summary>
public sealed class TestVehicleTableViewDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the vehicle.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the vehicle.
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
    /// Gets or sets the date and time the vehicle was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
