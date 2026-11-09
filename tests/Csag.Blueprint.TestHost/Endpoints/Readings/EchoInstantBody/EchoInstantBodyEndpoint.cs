namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstantBody;

using Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;
using FastEndpoints;

/// <summary>
/// Echoes instants from a JSON body, so tests can observe which values System.Text.Json accepts. Anonymous,
/// since serialization is all it exercises.
/// </summary>
public sealed class EchoInstantBodyEndpoint : Endpoint<EchoInstantRequest, EchoInstantResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/echo-instant");
        this.AllowAnonymous();
        this.Summary(s => s.Summary = "Echo instants from a JSON body");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoInstantRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoInstantResponse { At = req.At, Until = req.Until }, ct);
    }
}
