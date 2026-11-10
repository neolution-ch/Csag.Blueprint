namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoTimeOfDay;

/// <summary>
/// Times of day without an offset.
/// </summary>
public sealed class TimeOfDayMessage
{
    /// <summary>
    /// Gets or sets a required time of day.
    /// </summary>
    public TimeOnly Opens { get; set; }

    /// <summary>
    /// Gets or sets an optional time of day.
    /// </summary>
    public TimeOnly? Closes { get; set; }
}
