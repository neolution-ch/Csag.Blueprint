namespace Csag.Blueprint.TestHost.Endpoints.Auth.Sessions.List;

using Csag.Blueprint.Application.Abstractions.Services;
using FastEndpoints;

/// <summary>
/// Lists the sessions of the caller's account through <see cref="ISessionLifecycle.ListMySessionsAsync"/>.
/// </summary>
public sealed class ListMySessionsEndpoint : EndpointWithoutRequest<List<ActiveSessionInfo>>
{
    private readonly ISessionLifecycle sessionLifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListMySessionsEndpoint"/> class.
    /// </summary>
    /// <param name="sessionLifecycle">The session lifecycle.</param>
    public ListMySessionsEndpoint(ISessionLifecycle sessionLifecycle)
    {
        this.sessionLifecycle = sessionLifecycle;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/sessions");
        this.Summary(s => s.Summary = "List my active sessions");
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessions = await this.sessionLifecycle.ListMySessionsAsync();
        await this.Send.OkAsync(sessions.ToList(), ct);
    }
}
