namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoHeader;

using Csag.Blueprint.TestHost.Endpoints.Readings.Echo;
using FastEndpoints;

/// <summary>
/// Echoes local readings bound from request headers, so tests can observe which values the header binder
/// accepts. Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoHeaderReadingEndpoint : Endpoint<EchoHeaderReadingRequest, EchoReadingResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/echo-header");
        this.AllowAnonymous();
        this.Summary(s => s.Summary = "Echo local readings bound from headers");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoHeaderReadingRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoReadingResponse { At = req.At, Until = req.Until, AtKind = req.At.Kind }, ct);
    }
}
