namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.HttpsRedirect;

using Csag.Blueprint.Web.Options.Api.Security.HttpsRedirect;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="HttpsRedirectSettingsValidator"/>. All rules are gated on
/// <see cref="HttpsRedirectSettings.Enabled"/>, so disabled settings always pass.
/// </summary>
public sealed class HttpsRedirectSettingsValidatorTests
{
    private readonly HttpsRedirectSettingsValidator validator = new();

    [Fact]
    public void Validate_Disabled_IgnoresInvalidValues()
    {
        var settings = new HttpsRedirectSettings
        {
            Enabled = false,
            RedirectStatusCode = 0,
            HttpsPort = 0,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(301)]
    [InlineData(307)]
    [InlineData(308)]
    public void Validate_EnabledWithValidStatusCode_Passes(int statusCode)
    {
        var settings = new HttpsRedirectSettings
        {
            Enabled = true,
            RedirectStatusCode = statusCode,
            HttpsPort = null,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(302)]
    [InlineData(200)]
    public void Validate_EnabledWithInvalidStatusCode_Fails(int statusCode)
    {
        var settings = new HttpsRedirectSettings
        {
            Enabled = true,
            RedirectStatusCode = statusCode,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RedirectStatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(443)]
    [InlineData(65535)]
    public void Validate_EnabledWithValidHttpsPort_Passes(int httpsPort)
    {
        var settings = new HttpsRedirectSettings
        {
            Enabled = true,
            RedirectStatusCode = 308,
            HttpsPort = httpsPort,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_EnabledWithOutOfRangeHttpsPort_Fails(int httpsPort)
    {
        var settings = new HttpsRedirectSettings
        {
            Enabled = true,
            RedirectStatusCode = 308,
            HttpsPort = httpsPort,
        };

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "HttpsPort must be between 1 and 65535");
    }
}
