namespace Csag.Blueprint.Web.Options.Api.Security.HttpsRedirect
{
    using FluentValidation;

    /// <summary>
    /// Validator for HttpsRedirectSettings configuration.
    /// Ensures HTTPS redirection settings are valid.
    /// </summary>
    public sealed class HttpsRedirectSettingsValidator : AbstractValidator<HttpsRedirectSettings>
    {
        private static readonly int[] ValidRedirectStatusCodes = [301, 307, 308];

        public HttpsRedirectSettingsValidator()
        {
            this.When(x => x.Enabled, () =>
            {
                this.RuleFor(x => x.RedirectStatusCode)
                    .Must(code => ValidRedirectStatusCodes.Contains(code))
                    .WithMessage($"RedirectStatusCode must be one of: {string.Join(", ", ValidRedirectStatusCodes)} (301=Moved Permanently, 307=Temporary Redirect, 308=Permanent Redirect)");

                this.When(x => x.HttpsPort.HasValue, () =>
                {
                    this.RuleFor(x => x.HttpsPort!.Value)
                        .InclusiveBetween(1, 65535)
                        .WithMessage("HttpsPort must be between 1 and 65535");
                });
            });
        }
    }
}
