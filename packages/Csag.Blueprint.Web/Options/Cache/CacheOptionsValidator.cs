namespace Csag.Blueprint.Web.Options.Cache
{
    using FluentValidation;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Validator for <see cref="CacheOptions"/>.
    /// </summary>
    public sealed class CacheOptionsValidator : AbstractValidator<CacheOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheOptionsValidator"/> class.
        /// </summary>
        public CacheOptionsValidator(IConfiguration configuration)
        {
            this.RuleFor(x => x.Provider)
                .NotNull()
                .WithMessage("Blueprint:Cache:Provider must be set. Valid values are 'SqlServer' or 'Redis'")
                .IsInEnum()
                .WithMessage("Blueprint:Cache:Provider must be either 'SqlServer' or 'Redis'");

            this.When(x => x.Provider == CacheProvider.Redis, () =>
            {
                this.RuleFor(_ => configuration.GetConnectionString("Redis"))
                    .NotEmpty()
                    .WithMessage("ConnectionStrings:Redis must be set when CacheProvider is set to 'Redis'");
            });
        }
    }
}
