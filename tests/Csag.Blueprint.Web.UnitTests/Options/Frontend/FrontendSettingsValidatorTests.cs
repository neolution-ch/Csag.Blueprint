namespace Csag.Blueprint.Web.UnitTests.Options.Frontend;

using Csag.Blueprint.Web.Options.Frontend;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="FrontendSettingsValidator"/>, covering the optional base URL rule:
/// absolute http(s) origin with no path, query, or fragment.
/// </summary>
public sealed class FrontendSettingsValidatorTests
{
    private readonly FrontendSettingsValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingBaseUrl_Passes(string? baseUrl)
    {
        var settings = new FrontendSettings { BaseUrl = baseUrl };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:20023")]
    [InlineData("https://app.example.com/")] // Uri normalizes a bare trailing slash to AbsolutePath "/"
    public void Validate_ValidBaseUrl_Passes(string baseUrl)
    {
        var settings = new FrontendSettings { BaseUrl = baseUrl };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("https://app.example.com/some/path")]
    [InlineData("https://app.example.com?query=1")]
    [InlineData("https://app.example.com#fragment")]
    public void Validate_InvalidBaseUrl_Fails(string baseUrl)
    {
        var settings = new FrontendSettings { BaseUrl = baseUrl };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.BaseUrl)
            .WithErrorMessage("BaseUrl must be a valid absolute base URL with no path, query, or fragment (e.g. \"https://app.example.com\")");
    }
}
