namespace Csag.Blueprint.TestHost.Endpoints.Auth.SwitchTenant;

using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The outcome of a tenant switch.
/// </summary>
public sealed class SwitchTenantResponse
{
    /// <summary>
    /// Gets or sets what the switch did.
    /// </summary>
    public SessionTenantSwitchOutcome Outcome { get; set; }
}
