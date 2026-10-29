namespace Csag.Blueprint.TestHost.Plugin;

/// <summary>
/// Response for <see cref="ExternalMarkerEndpoint"/>.
/// </summary>
public sealed class ExternalMarkerResponse
{
    /// <summary>
    /// Gets or sets a fixed marker value, confirming the response came from this endpoint.
    /// </summary>
    public string Marker { get; set; } = string.Empty;
}
