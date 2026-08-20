namespace Csag.Blueprint.TestHost.Endpoints.Auth.Logout;

using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Ends the current session: signing out removes the server-side ticket (immediate revocation),
/// untracks the session row, and clears the cookie.
/// </summary>
public sealed class LogoutEndpoint : EndpointWithoutRequest<LogoutResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/logout");
        this.Summary(s =>
        {
            s.Summary = "Logout from the current session";
            s.Description = "Removes the server-side authentication ticket and clears the session cookie.";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        await this.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await this.Send.OkAsync(new LogoutResponse { Succeeded = true }, ct);
    }
}
