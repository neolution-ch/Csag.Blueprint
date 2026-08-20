namespace Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// Kind of <see cref="TestVehicle"/>. Exists to exercise enum-valued columns
/// (e.g. table view enum filters) in unit tests.
/// </summary>
public enum TestVehicleKind
{
    /// <summary>
    /// No kind assigned.
    /// </summary>
    None = 0,

    /// <summary>
    /// A bicycle.
    /// </summary>
    Bicycle = 1,

    /// <summary>
    /// A scooter.
    /// </summary>
    Scooter = 2,

    /// <summary>
    /// A kayak.
    /// </summary>
    Kayak = 3,
}
