namespace Csag.Blueprint.Web.Options.Api.Security.RequestLimits
{
    using FluentValidation;

    /// <summary>
    /// Validator for RequestLimitsSettings configuration.
    /// Ensures request body and multipart size limits are valid.
    /// </summary>
    public sealed class RequestLimitsSettingsValidator : AbstractValidator<RequestLimitsSettings>
    {
        public RequestLimitsSettingsValidator()
        {
            this.RuleFor(x => x.MaxRequestBodySizeMegabytes)
                .GreaterThan(0)
                .WithMessage("Blueprint:Security:RequestLimits:MaxRequestBodySizeMegabytes must be greater than 0");

            this.RuleFor(x => x.MultipartBodyLengthLimitMegabytes)
                .GreaterThan(0)
                .WithMessage("Blueprint:Security:RequestLimits:MultipartBodyLengthLimitMegabytes must be greater than 0")
                .LessThanOrEqualTo(x => x.MaxRequestBodySizeMegabytes)
                .WithMessage("Blueprint:Security:RequestLimits:MultipartBodyLengthLimitMegabytes must not exceed MaxRequestBodySizeMegabytes — a per-section limit above the total body cap is ineffective because Kestrel rejects the request first");
        }
    }
}
