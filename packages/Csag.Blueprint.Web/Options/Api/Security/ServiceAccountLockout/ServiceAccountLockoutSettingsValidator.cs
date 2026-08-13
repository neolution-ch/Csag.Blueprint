namespace Csag.Blueprint.Web.Options.Api.Security.ServiceAccountLockout
{
    using FluentValidation;

    /// <summary>
    /// Validator for <see cref="ServiceAccountLockoutSettings"/>.
    /// Ensures the service-account lockout thresholds are present and sensible.
    /// </summary>
    public sealed class ServiceAccountLockoutSettingsValidator : AbstractValidator<ServiceAccountLockoutSettings>
    {
        public ServiceAccountLockoutSettingsValidator()
        {
            this.RuleFor(x => x.MaxFailedAccessAttempts)
                .GreaterThanOrEqualTo(1)
                .WithMessage("MaxFailedAccessAttempts must be at least 1")
                .LessThanOrEqualTo(100)
                .WithMessage("MaxFailedAccessAttempts must not exceed 100");

            this.RuleFor(x => x.LockoutDurationMinutes)
                .GreaterThanOrEqualTo(1)
                .WithMessage("LockoutDurationMinutes must be at least 1")
                .LessThanOrEqualTo(1440)
                .WithMessage("LockoutDurationMinutes must not exceed 1440 (24 hours)");
        }
    }
}
