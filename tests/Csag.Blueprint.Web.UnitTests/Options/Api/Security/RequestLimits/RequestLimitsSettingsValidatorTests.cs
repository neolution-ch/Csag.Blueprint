namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.RequestLimits;

using Csag.Blueprint.Web.Options.Api.Security.RequestLimits;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="RequestLimitsSettingsValidator"/>, covering the positive-size rules
/// and the per-section limit never exceeding the total body cap.
/// </summary>
public sealed class RequestLimitsSettingsValidatorTests
{
    private readonly RequestLimitsSettingsValidator validator = new();

    [Fact]
    public void Validate_MultipartLimitBelowBodyLimit_Passes()
    {
        var settings = new RequestLimitsSettings
        {
            MaxRequestBodySizeMegabytes = 100,
            MultipartBodyLengthLimitMegabytes = 50,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MultipartLimitEqualToBodyLimit_Passes()
    {
        var settings = new RequestLimitsSettings
        {
            MaxRequestBodySizeMegabytes = 100,
            MultipartBodyLengthLimitMegabytes = 100,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveMaxRequestBodySize_Fails(int maxRequestBodySizeMegabytes)
    {
        var settings = new RequestLimitsSettings
        {
            MaxRequestBodySizeMegabytes = maxRequestBodySizeMegabytes,
            MultipartBodyLengthLimitMegabytes = 1,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.MaxRequestBodySizeMegabytes)
            .WithErrorMessage("Blueprint:Security:RequestLimits:MaxRequestBodySizeMegabytes must be greater than 0");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveMultipartBodyLengthLimit_Fails(int multipartBodyLengthLimitMegabytes)
    {
        var settings = new RequestLimitsSettings
        {
            MaxRequestBodySizeMegabytes = 100,
            MultipartBodyLengthLimitMegabytes = multipartBodyLengthLimitMegabytes,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.MultipartBodyLengthLimitMegabytes)
            .WithErrorMessage("Blueprint:Security:RequestLimits:MultipartBodyLengthLimitMegabytes must be greater than 0");
    }

    [Fact]
    public void Validate_MultipartLimitAboveBodyLimit_Fails()
    {
        var settings = new RequestLimitsSettings
        {
            MaxRequestBodySizeMegabytes = 100,
            MultipartBodyLengthLimitMegabytes = 101,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.MultipartBodyLengthLimitMegabytes);
    }
}
