namespace Csag.Blueprint.Web.Options;

using FluentValidation;
using Microsoft.Extensions.Options;

/// <summary>
/// Adapter that integrates FluentValidation with the IOptions pattern.
/// Validates options using a FluentValidation validator when the options are accessed.
/// </summary>
/// <typeparam name="TOptions">The options type to validate.</typeparam>
public sealed class FluentValidationOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IValidator<TOptions> validator;

    public FluentValidationOptions(IValidator<TOptions> validator)
    {
        this.validator = validator;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var validationResult = this.validator.Validate(options);

        if (validationResult.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = validationResult.Errors
            .Select(error => $"{error.PropertyName}: {error.ErrorMessage}");

        return ValidateOptionsResult.Fail(errors);
    }
}
