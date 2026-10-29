namespace Csag.Blueprint.TestHost.Endpoints.Readings.Echo;

/// <summary>
/// The local readings as bound, with the kind of the required one.
/// </summary>
public sealed class EchoReadingResponse
{
    /// <summary>
    /// Gets or sets the bound required reading.
    /// </summary>
    public DateTime At { get; set; }

    /// <summary>
    /// Gets or sets the bound optional reading.
    /// </summary>
    public DateTime? Until { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTime.Kind"/> of <see cref="At"/>.
    /// </summary>
    public DateTimeKind AtKind { get; set; }
}
