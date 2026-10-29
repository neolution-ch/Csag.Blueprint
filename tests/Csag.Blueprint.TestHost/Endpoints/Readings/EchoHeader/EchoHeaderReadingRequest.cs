namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoHeader;

using FastEndpoints;

/// <summary>
/// Local readings bound from request headers.
/// </summary>
public sealed class EchoHeaderReadingRequest
{
    /// <summary>
    /// Gets or sets a required local reading, from the <c>X-At</c> header.
    /// </summary>
    [FromHeader("X-At")]
    public DateTime At { get; set; }

    /// <summary>
    /// Gets or sets an optional local reading, from the <c>X-Until</c> header.
    /// </summary>
    [FromHeader("X-Until", IsRequired = false)]
    public DateTime? Until { get; set; }
}
