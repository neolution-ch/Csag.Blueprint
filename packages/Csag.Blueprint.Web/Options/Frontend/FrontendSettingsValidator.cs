namespace Csag.Blueprint.Web.Options.Frontend
{
    using FluentValidation;

    /// <summary>
    /// Validator for FrontendSettings configuration.
    /// Ensures the frontend base URL, when provided, is a valid absolute base URL without a path.
    /// </summary>
    public sealed class FrontendSettingsValidator : AbstractValidator<FrontendSettings>
    {
        public FrontendSettingsValidator()
        {
            this.RuleFor(x => x.BaseUrl)
                .Must(BeBaseUrlWithoutPath)
                .When(x => !string.IsNullOrEmpty(x.BaseUrl))
                .WithMessage("BaseUrl must be a valid absolute base URL with no path, query, or fragment (e.g. \"https://app.example.com\")");
        }

        private static bool BeBaseUrlWithoutPath(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.AbsolutePath == "/"
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }
    }
}
