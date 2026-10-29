namespace Csag.Blueprint.TestHost.Endpoints.Auth.SwitchTenant;

using Csag.Blueprint.Application.Abstractions.Services;
using FastEndpoints;

/// <summary>
/// Moves the caller's session to another tenant through <see cref="ISessionLifecycle.SwitchTenantAsync"/> and
/// reports the outcome.
/// </summary>
public sealed class SwitchTenantEndpoint : Endpoint<SwitchTenantRequest, SwitchTenantResponse>
{
    private readonly ISessionLifecycle sessionLifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchTenantEndpoint"/> class.
    /// </summary>
    /// <param name="sessionLifecycle">The session lifecycle.</param>
    public SwitchTenantEndpoint(ISessionLifecycle sessionLifecycle)
    {
        this.sessionLifecycle = sessionLifecycle;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/switch-tenant");
        this.Summary(s =>
        {
            s.Summary = "Switch the current session's tenant";
            s.Response<SwitchTenantResponse>(200, "The switch outcome");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(SwitchTenantRequest req, CancellationToken ct)
    {
        var outcome = await this.sessionLifecycle.SwitchTenantAsync(req.TenantId);
        await this.Send.OkAsync(new SwitchTenantResponse { Outcome = outcome }, ct);
    }
}
