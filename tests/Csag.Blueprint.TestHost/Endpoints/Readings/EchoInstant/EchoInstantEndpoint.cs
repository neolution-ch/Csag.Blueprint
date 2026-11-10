namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;

using FastEndpoints;

/// <summary>
/// Echoes instants bound from the query string, so tests can observe which values the query binder accepts.
/// Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoInstantEndpoint : Endpoint<EchoInstantRequest, EchoInstantResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/echo-instant");
        this.AllowAnonymous();
        this.Summary(s => s.Summary = "Echo instants bound from the query string");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoInstantRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoInstantResponse { At = req.At, Until = req.Until }, ct);
    }
}
