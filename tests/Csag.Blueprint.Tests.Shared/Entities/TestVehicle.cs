namespace Csag.Blueprint.Tests.Shared.Entities;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Tenant-owned, auditable sample business entity for unit tests. Its property shapes cover the
/// column kinds the table view infrastructure filters and sorts on: string (contains), bool,
/// enum, numeric range (int and decimal), and date/time values.
/// </summary>
public sealed class TestVehicle : IMustHaveTenant, IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the vehicle.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier that owns this vehicle.
    /// </summary>
    public Guid TenantId { get; set; }

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
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time the vehicle was acquired.
    /// </summary>
    public DateTime AcquiredAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the vehicle was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the vehicle was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <inheritdoc/>
    public string? CreatedByActor { get; set; }

    /// <inheritdoc/>
    public string? UpdatedByActor { get; set; }
}
