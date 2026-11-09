namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;

/// <summary>
/// The instants as bound.
/// </summary>
public sealed class EchoInstantResponse
{
    /// <summary>
    /// Gets or sets the bound required instant.
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets the bound optional instant.
    /// </summary>
    public DateTimeOffset? Until { get; set; }
}
