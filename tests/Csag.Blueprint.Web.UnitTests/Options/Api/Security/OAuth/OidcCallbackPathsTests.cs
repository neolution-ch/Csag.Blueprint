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

    [Theory]
    [InlineData("/api/auth/signin-google")]
    [InlineData("/api/signin-oidc/okta")]
    [InlineData("/api/auth/signin.google")]
    public void IsUnderProxiedPrefix_NormalizedPathUnderThePrefix_IsTrue(string path)
    {
        OidcCallbackPaths.IsUnderProxiedPrefix(path).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/signin-google")]
    [InlineData("/apifoo/signin-google")]
    [InlineData("/api")]
    [InlineData("/api/")]
    [InlineData("/api/../signin-google")]
    [InlineData("/api/%2e%2E/signin-google")]
    [InlineData("/api/%2e%2e%2fsignin-google")]
    [InlineData("/api/auth%5c..%5csignin-google")]
    [InlineData("/api/./signin-google")]
    [InlineData("/api//signin-google")]
    [InlineData("/api/auth/signin-google/")]
    [InlineData("/api/auth\\..\\signin-google")]
    [InlineData("/api/auth/signin-google?x=1")]
    [InlineData("/api/auth/signin-google#fragment")]
    public void IsUnderProxiedPrefix_PathOutsideThePrefixOrNotNormalized_IsFalse(string path)
    {
        OidcCallbackPaths.IsUnderProxiedPrefix(path).ShouldBeFalse();
    }
}
