namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// Entity holding each kind of time value, once required and once optional, plus time values nested in
/// another complex type, in one mapped to JSON, and in an owned type.
/// </summary>
public sealed class Appointment
{
    public Guid Id { get; set; }

    public PlaceTime Start { get; set; } = null!;

    public PlaceTime? End { get; set; }

    public ZonedPlaceTime StandOpens { get; set; } = null!;

    public ZonedPlaceTime? StandCloses { get; set; }

    public ViewerTime Briefing { get; set; } = null!;

    public ViewerTime? Debriefing { get; set; }

    public AppointmentSlot Slot { get; set; } = new();

    public AppointmentSlot Archive { get; set; } = new();

    public AppointmentVenue Venue { get; set; } = new();
}
