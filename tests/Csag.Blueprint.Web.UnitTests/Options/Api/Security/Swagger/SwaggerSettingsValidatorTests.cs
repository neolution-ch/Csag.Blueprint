namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Swagger;

using Csag.Blueprint.Web.Options.Api.Security.Swagger;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="SwaggerSettingsValidator"/>. The validator intentionally carries no
/// rules (a single boolean flag), so both states must pass.
/// </summary>
public sealed class SwaggerSettingsValidatorTests
{
    private readonly SwaggerSettingsValidator validator = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_AnyEnabledState_Passes(bool enabled)
    {
        var settings = new SwaggerSettings { Enabled = enabled };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
