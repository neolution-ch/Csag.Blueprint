namespace Csag.Blueprint.TestHost.Endpoints.Auth.Login;

using FastEndpoints;
using FluentValidation;

/// <summary>
/// Validates the login request before credentials are checked.
/// </summary>
public sealed class LoginValidator : Validator<LoginRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginValidator"/> class.
    /// </summary>
    public LoginValidator()
    {
        this.RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        this.RuleFor(x => x.Password)
            .NotEmpty();
    }
}
