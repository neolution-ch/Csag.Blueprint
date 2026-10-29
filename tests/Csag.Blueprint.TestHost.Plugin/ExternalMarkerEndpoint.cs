namespace Csag.Blueprint.TestHost.Plugin;

using FastEndpoints;

/// <summary>
/// Lives outside the TestHost entry assembly, in its own project. TestHost registers this
/// assembly through <c>BlueprintServicesOptions.FastEndpointsAdditionalAssemblies</c>. A passing
/// route pins the additive-discovery contract. This assembly's endpoint is found alongside the
/// host's own endpoints. The host's own endpoints still work too.
/// </summary>
public sealed class ExternalMarkerEndpoint : EndpointWithoutRequest<ExternalMarkerResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/external-marker");
        this.AllowAnonymous();
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        await this.Send.OkAsync(new ExternalMarkerResponse { Marker = "external-marker" }, ct);
    }
}
