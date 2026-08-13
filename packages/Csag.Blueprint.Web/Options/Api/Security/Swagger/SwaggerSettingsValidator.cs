namespace Csag.Blueprint.Web.Options.Api.Security.Swagger
{
    using FluentValidation;

    /// <summary>
    /// Validator for SwaggerSettings configuration.
    /// Ensures Swagger settings are valid.
    /// </summary>
    public sealed class SwaggerSettingsValidator : AbstractValidator<SwaggerSettings>
    {
#pragma warning disable S3253 // Constructor and destructor declarations should not be redundant
        public SwaggerSettingsValidator()
        {
            // Enabled is a boolean flag with no specific validation rules beyond type safety.
            // Validator is included for consistency with the pattern and future extensibility.
        }
#pragma warning restore S3253 // Constructor and destructor declarations should not be redundant
    }
}
