namespace Csag.Blueprint.Web.UnitTests.Options.FeatureFlags;

using Csag.Blueprint.Web.Options.FeatureFlags;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="FeatureFlagOptionsValidator"/>. The validator intentionally carries
/// no rules (boolean flags only), so every flag combination must pass.
/// </summary>
public sealed class FeatureFlagOptionsValidatorTests
{
    private readonly FeatureFlagOptionsValidator validator = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Validate_AnyFlagCombination_Passes(bool enableConfigurationExample, bool enableSimulateError)
    {
        var options = new FeatureFlagOptions
        {
            EnableConfigurationExample = enableConfigurationExample,
            EnableSimulateError = enableSimulateError,
        };

        var result = this.validator.TestValidate(options);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
