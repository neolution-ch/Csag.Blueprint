namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Moq;

// Aliased because Microsoft.AspNetCore.Authentication.Cookies ships a type of the same name.
using BlueprintPostConfigure = Csag.Blueprint.Infrastructure.Session.PostConfigureCookieAuthenticationOptions;

/// <summary>
/// Unit tests for <see cref="BlueprintPostConfigure"/> scheme scoping. The ticket
/// store must reach the Identity application cookie and nothing else: the external and two-factor
/// cookies have no tracking rows, so installing it there would make every one of their removals
/// delete a row that never existed.
/// </summary>
public sealed class PostConfigureCookieAuthenticationOptionsTests
{
    [Fact]
    public void PostConfigure_ApplicationScheme_InstallsTicketStore()
    {
        var ticketStore = new Mock<ITicketStore>();
        var postConfigure = new BlueprintPostConfigure(ticketStore.Object);
        var options = new CookieAuthenticationOptions();

        postConfigure.PostConfigure(IdentityConstants.ApplicationScheme, options);

        options.SessionStore.ShouldBeSameAs(ticketStore.Object);
    }

    [Theory]
    [InlineData("Identity.External")]
    [InlineData("Identity.TwoFactorUserId")]
    [InlineData("Identity.TwoFactorRememberMe")]
    [InlineData(null)]
    public void PostConfigure_OtherSchemes_LeavesTicketStoreUnset(string? scheme)
    {
        var ticketStore = new Mock<ITicketStore>();
        var postConfigure = new BlueprintPostConfigure(ticketStore.Object);
        var options = new CookieAuthenticationOptions();

        postConfigure.PostConfigure(scheme, options);

        options.SessionStore.ShouldBeNull();
    }
}
