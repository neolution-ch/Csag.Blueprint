namespace Csag.Blueprint.Web.Options.Api.Security.Jwt
{
    using FluentValidation;

    /// <summary>
    /// Validator for <see cref="JwtSettings"/> configuration.
    /// Note: SigningKey <b>presence</b> is not enforced here because SecuritySettings validation runs
    /// unconditionally (including generation mode, where no signing key is configured). Presence is
    /// asserted by the host when it registers its runtime services, gated behind its generation-mode
    /// check. A key that is provided must still meet the HS256 minimum length in every mode.
    /// </summary>
    public sealed class JwtSettingsValidator : AbstractValidator<JwtSettings>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JwtSettingsValidator"/> class.
        /// </summary>
        public JwtSettingsValidator()
        {
            this.RuleFor(x => x.Issuer)
                .NotEmpty()
                .WithMessage("JWT issuer must not be empty.");

            this.RuleFor(x => x.Audience)
                .NotEmpty()
                .WithMessage("JWT audience must not be empty.");

            this.RuleFor(x => x.ExpirationHours)
                .GreaterThan(0)
                .WithMessage("JWT token expiration must be greater than 0 hours.");

            // Only the strength of a provided key is checked; an absent key is the generation-mode
            // case and is left to the host's runtime-gated presence check (see the class doc).
            this.RuleFor(x => x.SigningKey)
                .MinimumLength(32)
                .WithMessage("JWT signing key must be at least 32 characters long for HS256 security.")
                .When(x => !string.IsNullOrEmpty(x.SigningKey));
        }
    }
}
