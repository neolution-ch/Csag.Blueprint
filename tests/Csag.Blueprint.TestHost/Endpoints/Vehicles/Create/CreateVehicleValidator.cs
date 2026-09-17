namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;

using FastEndpoints;
using FluentValidation;

/// <summary>
/// Validates the create-vehicle payload. Violations surface as a 400 ProblemDetails response with
/// one error entry per failed rule.
/// </summary>
public sealed class CreateVehicleValidator : Validator<CreateVehicleRequest>
{
    /// <summary>
    /// The maximum accepted length of a vehicle name.
    /// </summary>
    internal const int NameMaxLength = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVehicleValidator"/> class.
    /// </summary>
    public CreateVehicleValidator()
    {
        this.RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Name must not exceed {NameMaxLength} characters.");

        this.RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Kind must be a defined vehicle kind.");

        this.RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Capacity must be greater than 0.");

        this.RuleFor(x => x.PricePerHour)
            .GreaterThanOrEqualTo(0)
            .WithMessage("PricePerHour must not be negative.");
    }
}
