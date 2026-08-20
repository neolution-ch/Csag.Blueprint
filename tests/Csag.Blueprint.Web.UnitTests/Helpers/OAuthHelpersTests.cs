namespace Csag.Blueprint.Web.UnitTests.Helpers;

using Csag.Blueprint.Web.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="OAuthHelpers"/>: the open-redirect guard <see cref="OAuthHelpers.ValidateLocalPath"/>
/// and <see cref="OAuthHelpers.BuildPostAuthRedirect"/>, which routes the post-login redirect back to the
/// frontend origin for split frontend/API deployments.
/// </summary>
public sealed class OAuthHelpersTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("/nested/path?q=1&r=2", "/nested/path?q=1&r=2")]
    public void ValidateLocalPath_LocalPath_ReturnsItUnchanged(string input, string expected)
    {
        OAuthHelpers.ValidateLocalPath(input, NullLogger.Instance).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("//evil.com")] // protocol-relative
    [InlineData("/\\evil.com")] // backslash form — browsers normalise "\" to "/"
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com")]
    [InlineData("evil.com")]
    [InlineData("javascript:alert(1)")]
    public void ValidateLocalPath_NonLocalOrEmpty_FallsBackToRoot(string? input)
    {
        OAuthHelpers.ValidateLocalPath(input, NullLogger.Instance).ShouldBe("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPostAuthRedirect_NoFrontendBaseUrl_ReturnsLocalPath(string? frontendBaseUrl)
    {
        OAuthHelpers.BuildPostAuthRedirect(frontendBaseUrl, "/dashboard").ShouldBe("/dashboard");
    }

    [Fact]
    public void BuildPostAuthRedirect_WithFrontendBaseUrl_ReturnsAbsoluteUrl()
    {
        OAuthHelpers.BuildPostAuthRedirect("https://app.example.com", "/dashboard")
            .ShouldBe("https://app.example.com/dashboard");
    }

    [Fact]
    public void BuildPostAuthRedirect_TrimsTrailingSlashOnBaseUrl()
    {
        OAuthHelpers.BuildPostAuthRedirect("https://app.example.com/", "/dashboard")
            .ShouldBe("https://app.example.com/dashboard");
    }
}
