namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstantHeader;

using Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;
using FastEndpoints;

/// <summary>
/// Echoes instants bound from request headers, so tests can observe which values the header binder accepts.
/// Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoInstantHeaderEndpoint : Endpoint<EchoInstantHeaderRequest, EchoInstantResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/echo-instant-header");
        this.AllowAnonymous();
        this.Summary(s => s.Summary = "Echo instants bound from headers");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoInstantHeaderRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoInstantResponse { At = req.At, Until = req.Until }, ct);
    }
}
