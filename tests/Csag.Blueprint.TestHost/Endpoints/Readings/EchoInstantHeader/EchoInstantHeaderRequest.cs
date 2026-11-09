namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstantHeader;

using FastEndpoints;

/// <summary>
/// Instants bound from request headers.
/// </summary>
public sealed class EchoInstantHeaderRequest
{
    /// <summary>
    /// Gets or sets a required instant, from the <c>X-At</c> header.
    /// </summary>
    [FromHeader("X-At")]
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets an optional instant, from the <c>X-Until</c> header.
    /// </summary>
    [FromHeader("X-Until", IsRequired = false)]
    public DateTimeOffset? Until { get; set; }
}
