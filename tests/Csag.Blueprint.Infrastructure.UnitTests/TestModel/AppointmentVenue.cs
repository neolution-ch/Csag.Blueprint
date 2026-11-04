namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// An owned type holding a time value, stored in its owner's table.
/// </summary>
public sealed class AppointmentVenue
{
    public string Name { get; set; } = string.Empty;

    public PlaceTime? DoorsOpen { get; set; }
}
