namespace Csag.Blueprint.TestHost.Endpoints.Auth.Sessions.Revoke;

using Csag.Blueprint.Application.Abstractions.Services;
using FastEndpoints;

/// <summary>
/// Ends one session of the caller's account through <see cref="ISessionLifecycle.RevokeMySessionAsync"/>.
/// </summary>
public sealed class RevokeMySessionEndpoint : EndpointWithoutRequest
{
    private readonly ISessionLifecycle sessionLifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeMySessionEndpoint"/> class.
    /// </summary>
    /// <param name="sessionLifecycle">The session lifecycle.</param>
    public RevokeMySessionEndpoint(ISessionLifecycle sessionLifecycle)
    {
        this.sessionLifecycle = sessionLifecycle;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Delete("/[namespace]/sessions/{sessionId:guid}");
        this.Summary(s =>
        {
            s.Summary = "Revoke one of my sessions";
            s.Response(204, "The session is ended");
            s.Response(404, "The caller's account has no session with that id");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await this.sessionLifecycle.RevokeMySessionAsync(this.Route<Guid>("sessionId")))
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.NoContentAsync(ct);
    }
}
