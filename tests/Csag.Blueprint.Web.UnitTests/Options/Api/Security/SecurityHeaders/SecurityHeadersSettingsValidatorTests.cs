namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.SecurityHeaders;

using Csag.Blueprint.Web.Options.Api.Security.SecurityHeaders;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="SecurityHeadersSettingsValidator"/>. The validator intentionally
/// carries no rules (boolean flags only), so every flag combination must pass.
/// </summary>
public sealed class SecurityHeadersSettingsValidatorTests
{
    private readonly SecurityHeadersSettingsValidator validator = new();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void Validate_AnyFlagCombination_Passes(bool enableHsts, bool enableSecurityHeaders, bool removeServerIdentityHeaders)
    {
        var settings = new SecurityHeadersSettings
        {
            EnableHsts = enableHsts,
            EnableSecurityHeaders = enableSecurityHeaders,
            RemoveServerIdentityHeaders = removeServerIdentityHeaders,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
