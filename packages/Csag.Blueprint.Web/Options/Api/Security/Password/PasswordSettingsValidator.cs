namespace Csag.Blueprint.Web.Options.Api.Security.Password
{
    using FluentValidation;

    /// <summary>
    /// Validator for PasswordSettings configuration.
    /// Ensures password policy settings are valid and sensible.
    /// </summary>
    public sealed class PasswordSettingsValidator : AbstractValidator<PasswordSettings>
    {
        public PasswordSettingsValidator()
        {
            this.RuleFor(x => x.RequiredLength)
                .GreaterThanOrEqualTo(1)
                .WithMessage("RequiredLength must be at least 1")
                .LessThanOrEqualTo(256)
                .WithMessage("RequiredLength must not exceed 256");

            this.RuleFor(x => x.RequiredUniqueChars)
                .GreaterThanOrEqualTo(1)
                .WithMessage("RequiredUniqueChars must be at least 1")
                .LessThanOrEqualTo(256)
                .WithMessage("RequiredUniqueChars must not exceed 256");

            this.RuleFor(x => x.RequiredUniqueChars)
                .LessThanOrEqualTo(x => x.RequiredLength)
                .WithMessage("RequiredUniqueChars cannot be greater than RequiredLength");
        }
    }
}
