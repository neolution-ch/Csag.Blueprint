namespace Csag.Blueprint.TestHost.Endpoints.Readings.Echo;

using FastEndpoints;

/// <summary>
/// Echoes local readings bound from the query string, so tests can observe which values the query binder
/// accepts. Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoReadingEndpoint : Endpoint<EchoReadingRequest, EchoReadingResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/echo");
        this.AllowAnonymous();
        this.Summary(s => s.Summary = "Echo local readings bound from the query string");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoReadingRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoReadingResponse { At = req.At, Until = req.Until, AtKind = req.At.Kind }, ct);
    }
}
