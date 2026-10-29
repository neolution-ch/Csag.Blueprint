namespace Csag.Blueprint.TestHost.Endpoints.Auth.ChangePassword;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Changes the caller's password, then reports the credential change to the session lifecycle after the change is
/// committed, which ends the account's other sessions and keeps the caller's.
/// </summary>
public sealed class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest>
{
    private readonly UserManager<TestUser> userManager;
    private readonly ISessionLifecycle sessionLifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordEndpoint"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="sessionLifecycle">The session lifecycle.</param>
    public ChangePasswordEndpoint(UserManager<TestUser> userManager, ISessionLifecycle sessionLifecycle)
    {
        this.userManager = userManager;
        this.sessionLifecycle = sessionLifecycle;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/change-password");
        this.Summary(s =>
        {
            s.Summary = "Change the caller's password";
            s.Response(204, "Password changed; the account's other sessions are ended");
            s.Response(400, "The current password is wrong or the new one is rejected");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            await this.Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await this.userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            this.ThrowError(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await this.sessionLifecycle.CredentialsChangedAsync(user.Id);
        await this.Send.NoContentAsync(ct);
    }
}
