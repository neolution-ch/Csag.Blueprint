namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.OAuth;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;

/// <summary>
/// Unit tests for <see cref="OidcCallbackPaths"/>.
/// </summary>
public sealed class OidcCallbackPathsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NoCallbackPath_DefaultsUnderTheProxiedPrefix(string? callbackPath)
    {
        var path = OidcCallbackPaths.Resolve("okta", new OidcProviderSettings { CallbackPath = callbackPath });

        path.ShouldBe("/api/auth/signin-oidc/okta");
        path.ShouldStartWith(OidcCallbackPaths.ProxiedPrefix);
    }

    [Fact]
    public void Resolve_ConfiguredCallbackPath_IsUsedAsIs()
    {
        var path = OidcCallbackPaths.Resolve("google", new OidcProviderSettings { CallbackPath = "/api/auth/signin-google" });

        path.ShouldBe("/api/auth/signin-google");
    }
}
