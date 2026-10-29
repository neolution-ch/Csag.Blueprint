namespace Csag.Blueprint.TestHost.Endpoints.Readings.EchoTimeOfDay;

using FastEndpoints;

/// <summary>
/// Echoes times of day from a JSON body, so the OpenAPI document describes a <see cref="TimeOnly"/> in a
/// request and a response. Anonymous, since serialization is all it exercises.
/// </summary>
public sealed class EchoTimeOfDayEndpoint : Endpoint<TimeOfDayMessage, TimeOfDayMessage>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/echo-time-of-day");
        this.AllowAnonymous();
        this.Summary(s =>
        {
            s.Summary = "Echo times of day from a JSON body";
            s.ExampleRequest = new TimeOfDayMessage { Opens = new TimeOnly(9, 30), Closes = new TimeOnly(17, 0, 0, 500) };
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(TimeOfDayMessage req, CancellationToken ct)
    {
        await this.Send.OkAsync(req, ct);
    }
}
