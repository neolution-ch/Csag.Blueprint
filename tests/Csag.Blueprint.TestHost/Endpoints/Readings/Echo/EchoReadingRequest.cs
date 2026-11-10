namespace Csag.Blueprint.TestHost.Endpoints.Readings.Echo;

/// <summary>
/// Local readings bound from the query string rather than a JSON body.
/// </summary>
public sealed class EchoReadingRequest
{
    /// <summary>
    /// Gets or sets a required local reading.
    /// </summary>
    public DateTime At { get; set; }

    /// <summary>
    /// Gets or sets an optional local reading.
    /// </summary>
    public DateTime? Until { get; set; }
}
