namespace Csag.Blueprint.Tests.Shared.Entities;

using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Tenant-owned sample entity holding each kind of time value, once required and once optional: a place
/// time in the tenant's zone, a place time at a stand with its own zone, and a viewer time an organiser set.
/// </summary>
public sealed class TestAppointment : IMustHaveTenant
{
    /// <summary>
    /// Gets or sets the unique identifier for the appointment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier that owns this appointment.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the title of the appointment.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the appointment starts, at the tenant's site.
    /// </summary>
    public PlaceTime Start { get; set; } = null!;

    /// <summary>
    /// Gets or sets when the appointment ends, at the tenant's site, if it has a fixed end.
    /// </summary>
    public PlaceTime? End { get; set; }

    /// <summary>
    /// Gets or sets when the stand at a venue with its own zone opens.
    /// </summary>
    public ZonedPlaceTime StandOpens { get; set; } = null!;

    /// <summary>
    /// Gets or sets when the stand closes, if it has a fixed closing time.
    /// </summary>
    public ZonedPlaceTime? StandCloses { get; set; }

    /// <summary>
    /// Gets or sets when the online briefing an organiser set starts.
    /// </summary>
    public ViewerTime Briefing { get; set; } = null!;

    /// <summary>
    /// Gets or sets when the online debriefing starts, if one is planned.
    /// </summary>
    public ViewerTime? Debriefing { get; set; }
}
