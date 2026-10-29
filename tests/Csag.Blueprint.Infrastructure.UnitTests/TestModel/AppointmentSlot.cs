namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using System.ComponentModel.DataAnnotations.Schema;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// An application complex type holding a time value.
/// </summary>
[ComplexType]
public sealed class AppointmentSlot
{
    public string Room { get; set; } = string.Empty;

    public PlaceTime? Opens { get; set; }
}
