namespace Csag.Blueprint.TestHost.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The host's answer to "may this account hold a session?": an account that exists and is not locked out may.
/// </summary>
/// <param name="userManager">The Identity user manager the answer is read from.</param>
public sealed class TestHostSessionEligibility(UserManager<TestUser> userManager) : ISessionEligibility
{
    /// <inheritdoc/>
    public async Task<bool> MayHoldSessionAsync(Guid accountId)
    {
        var user = await userManager.FindByIdAsync(accountId.ToString());
        return user is not null && !await userManager.IsLockedOutAsync(user);
    }
}
