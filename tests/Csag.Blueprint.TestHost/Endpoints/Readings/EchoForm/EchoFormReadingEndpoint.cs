namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoForm;

using Csag.Blueprint.TestHost.Endpoints.Readings.Echo;
using FastEndpoints;

/// <summary>
/// Echoes local readings bound from form fields, so tests can observe which values the form binder
/// accepts. Anonymous, since binding is all it exercises.
/// </summary>
public sealed class EchoFormReadingEndpoint : Endpoint<EchoReadingRequest, EchoReadingResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/echo-form");
        this.AllowAnonymous();
        this.AllowFormData();
        this.Summary(s => s.Summary = "Echo local readings bound from form fields");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(EchoReadingRequest req, CancellationToken ct)
    {
        await this.Send.OkAsync(new EchoReadingResponse { At = req.At, Until = req.Until, AtKind = req.At.Kind }, ct);
    }
}
