namespace Csag.Blueprint.TestHost.Endpoints.Auth.WhoAmI;

using Csag.Blueprint.Infrastructure.Authentication;
using FastEndpoints;

/// <summary>
/// Describes the caller's principal, accepting bearer tokens only: the endpoint declares its own scheme, which
/// replaces the host's default rather than adding to it.
/// </summary>
public sealed class WhoAmIBearerEndpoint : EndpointWithoutRequest<WhoAmIResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/whoami/bearer");
        this.AuthSchemes(BlueprintAuthenticationSchemes.Bearer);
        this.Summary(s => s.Summary = "Describe the caller's principal, for bearer tokens only");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        await this.Send.OkAsync(WhoAmIResponse.From(this.User), ct);
    }
}
