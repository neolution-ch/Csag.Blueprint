namespace Csag.Blueprint.Web.Options.Api.Security
{
    using FluentValidation;

    /// <summary>
    /// Validator for <see cref="MfaSettings"/> configuration.
    /// Enforces the invariant that MFA can only be required when it is also supported.
    /// </summary>
    public sealed class MfaSettingsValidator : AbstractValidator<MfaSettings>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MfaSettingsValidator"/> class.
        /// </summary>
        public MfaSettingsValidator()
        {
            this.RuleFor(x => x.BypassExternalProviders)
                .NotNull()
                .WithMessage("Mfa.BypassExternalProviders cannot be null");

            // MFA cannot be required unless it is supported in the first place.
            this.RuleFor(x => x.Required)
                .Must((settings, required) => !required || settings.Enabled)
                .WithMessage("Mfa.Required can only be true when Mfa.Enabled is also true.");

            // Recovery codes are only generated once MFA can be enabled, so the count must be positive in that case.
            this.RuleFor(x => x.RecoveryCodeCount)
                .GreaterThan(0)
                .When(x => x.Enabled)
                .WithMessage("Mfa.RecoveryCodeCount must be greater than 0 when Mfa.Enabled is true.");

            // The issuer is embedded in every otpauth:// URI; an empty value yields a malformed URI (and
            // BuildAuthenticatorUri throws at request time), so require it when MFA is supported.
            this.RuleFor(x => x.Issuer)
                .NotEmpty()
                .When(x => x.Enabled)
                .WithMessage("Mfa.Issuer must not be empty when Mfa.Enabled is true.");
        }
    }
}
