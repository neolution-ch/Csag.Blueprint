namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstantForm;

using Csag.Blueprint.TestHost.Endpoints.Readings.EchoInstant;
using FastEndpoints;

/// <summary>
/// Echoes instants bound from form fields, so tests can observe which values the form binder accepts.
/// Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoInstantFormEndpoint : Endpoint<EchoInstantRequest, EchoInstantResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/echo-instant-form");
        this.AllowAnonymous();
        this.AllowFormData();
        this.Summary(s => s.Summary = "Echo instants bound from form fields");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoInstantRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoInstantResponse { At = req.At, Until = req.Until }, ct);
    }
}
