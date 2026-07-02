namespace Csag.Blueprint.Web.Options.FeatureFlags
{
    using FluentValidation;

    /// <summary>
    /// Validator for FeatureFlagOptions configuration.
    /// </summary>
    public sealed class FeatureFlagOptionsValidator : AbstractValidator<FeatureFlagOptions>
    {
#pragma warning disable S3253 // Constructor and destructor declarations should not be redundant
        public FeatureFlagOptionsValidator()
        {
            // Boolean properties don't require validation
        }
#pragma warning restore S3253 // Constructor and destructor declarations should not be redundant
    }
}
