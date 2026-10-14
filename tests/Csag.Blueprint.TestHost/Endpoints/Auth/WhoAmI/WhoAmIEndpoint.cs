namespace Csag.Blueprint.TestHost.Endpoints.Auth.WhoAmI;

using FastEndpoints;

/// <summary>
/// Describes the caller's principal. It declares no scheme, so it gets the host's default one — the per-request
/// selector between the session cookie and bearer tokens — and shows which identity the request authenticated as.
/// </summary>
public sealed class WhoAmIEndpoint : EndpointWithoutRequest<WhoAmIResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/whoami");
        this.Summary(s => s.Summary = "Describe the caller's principal");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        await this.Send.OkAsync(WhoAmIResponse.From(this.User), ct);
    }
}
