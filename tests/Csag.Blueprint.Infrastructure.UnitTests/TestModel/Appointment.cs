namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Time;

/// <summary>
/// Entity holding each kind of time value, once required and once optional, plus time values nested in
/// another complex type, in one mapped to JSON, and in an owned type, and plain instants, two of them with a
/// conversion of their own.
/// </summary>
public sealed class Appointment
{
    public Guid Id { get; set; }

    public PlaceTime Start { get; set; } = null!;

    public PlaceTime? End { get; set; }

    public ZonedTime StandOpens { get; set; } = null!;

    public ZonedTime? StandCloses { get; set; }

    public ZonedTime Briefing { get; set; } = null!;

    public ZonedTime? Debriefing { get; set; }

    public DateTimeOffset BookedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset RemindedAt { get; set; }

    public DateTimeOffset? ExportedAt { get; set; }

    public AppointmentSlot Slot { get; set; } = new();

    public AppointmentSlot Archive { get; set; } = new();

    public AppointmentVenue Venue { get; set; } = new();
}
