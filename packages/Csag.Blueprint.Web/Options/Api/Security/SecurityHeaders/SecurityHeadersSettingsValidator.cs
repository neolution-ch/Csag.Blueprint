namespace Csag.Blueprint.Web.Options.Api.Security.SecurityHeaders
{
    using FluentValidation;

    /// <summary>
    /// Validator for SecurityHeadersSettings configuration.
    /// Ensures security headers settings are valid.
    /// </summary>
    public sealed class SecurityHeadersSettingsValidator : AbstractValidator<SecurityHeadersSettings>
    {
#pragma warning disable S3253 // Constructor and destructor declarations should not be redundant
        public SecurityHeadersSettingsValidator()
        {
            // These are all boolean flags with no specific validation rules beyond type safety
            // Validator is included for consistency with the pattern and future extensibility
        }
#pragma warning restore S3253 // Constructor and destructor declarations should not be redundant
    }
}
