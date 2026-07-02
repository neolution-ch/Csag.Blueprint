namespace Csag.Blueprint.Web.Options.Api.Security.Cors
{
    using FluentValidation;

    /// <summary>
    /// Validator for CorsSettings configuration.
    /// Ensures CORS settings are valid and secure.
    /// </summary>
    public sealed class CorsSettingsValidator : AbstractValidator<CorsSettings>
    {
        private const int OneDayInSeconds = 86400;

        public CorsSettingsValidator()
        {
            this.When(x => !string.IsNullOrWhiteSpace(x.AllowedOrigins), () =>
            {
                this.RuleFor(x => x.AllowedOrigins)
                    .Must(BeValidOrigins!)
                    .WithMessage("AllowedOrigins must contain valid URLs without trailing slashes, separated by semicolons");
            });

            this.RuleFor(x => x)
                .Must(corsSettings => !corsSettings.AllowCredentials || !string.IsNullOrWhiteSpace(corsSettings.AllowedOrigins))
                .WithMessage("AllowCredentials cannot be true when AllowedOrigins is null or empty. Specify explicit origins for security.")
                .Must(corsSettings => !corsSettings.AllowCredentials || (corsSettings.AllowedOrigins != null && !corsSettings.AllowedOrigins.Contains('*', StringComparison.Ordinal)))
                .WithMessage("AllowCredentials cannot be true when AllowedOrigins contains wildcard '*'. This is a security risk. Specify explicit origins.");

            this.RuleFor(x => x.PreflightMaxAgeSeconds)
                .GreaterThan(0)
                .WithMessage("PreflightMaxAgeSeconds must be greater than 0")
                .LessThanOrEqualTo(OneDayInSeconds)
                .WithMessage("PreflightMaxAgeSeconds cannot exceed 86400 seconds (24 hours)");
        }

        private static bool BeValidOrigins(string origins)
        {
            var originList = origins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var origin in originList)
            {
                if (origin == "*")
                {
                    continue;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                // Only allow http and https schemes
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    return false;
                }

                // Ensure no trailing slash
                if (origin.EndsWith('/'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
