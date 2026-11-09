namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;

/// <summary>
/// Instants bound from the query string, form fields or a JSON body.
/// </summary>
public sealed class EchoInstantRequest
{
    /// <summary>
    /// Gets or sets a required instant.
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets an optional instant.
    /// </summary>
    public DateTimeOffset? Until { get; set; }
}
