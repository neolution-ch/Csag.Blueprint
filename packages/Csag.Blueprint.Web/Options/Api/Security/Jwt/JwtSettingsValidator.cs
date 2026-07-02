namespace Csag.Blueprint.Web.Options.Api.Security.Jwt
{
    using FluentValidation;

    /// <summary>
    /// Validator for <see cref="JwtSettings"/> configuration.
    /// Note: SigningKey is NOT validated here because SecuritySettings validation runs unconditionally
    /// (including generation mode where no signing key is available). SigningKey validation
    /// is performed manually in Program.cs, gated behind IsNotGenerationMode().
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

            this.RuleFor(x => x.SigningKey)
                .NotEmpty()
                .MinimumLength(32)
                .WithMessage("JWT signing key must be at least 32 characters long for HS256 security.");
        }
    }
}
