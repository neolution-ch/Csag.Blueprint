namespace Csag.Blueprint.TestHost.Endpoints.Auth.SwitchLanguage;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Changes the caller's preferred language, then reports the claims change to the session lifecycle after the change
/// is committed, which rebuilds the cached claims of the account's sessions, the caller's included.
/// </summary>
public sealed class SwitchLanguageEndpoint : Endpoint<SwitchLanguageRequest>
{
    private readonly UserManager<TestUser> userManager;
    private readonly ISessionLifecycle sessionLifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchLanguageEndpoint"/> class.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="sessionLifecycle">The session lifecycle.</param>
    public SwitchLanguageEndpoint(UserManager<TestUser> userManager, ISessionLifecycle sessionLifecycle)
    {
        this.userManager = userManager;
        this.sessionLifecycle = sessionLifecycle;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]/switch-language");
        this.Summary(s =>
        {
            s.Summary = "Change the caller's preferred language";
            s.Response(204, "Language changed; the account's sessions carry it");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(SwitchLanguageRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var user = await this.userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            await this.Send.UnauthorizedAsync(ct);
            return;
        }

        user.PreferredLanguage = req.Language;
        var result = await this.userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            this.ThrowError(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await this.sessionLifecycle.ClaimsChangedAsync(user.Id);
        await this.Send.NoContentAsync(ct);
    }
}
