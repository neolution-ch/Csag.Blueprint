namespace Csag.Blueprint.Web.UnitTests.Validation;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Password;
using Csag.Blueprint.Web.Validation;
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

// Inside this namespace the bare identifier "Options" binds to the Csag.Blueprint.Web.Options
// namespace, so the Microsoft options factory needs an explicit alias.
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

/// <summary>
/// Unit tests for <see cref="PasswordRuleBuilderExtensions.WithStrongPasswordRules{T}"/>, exercising
/// the settings-driven length and complexity branches through a throwaway model validator.
/// </summary>
public sealed class PasswordRuleBuilderExtensionsTests
{
    [Fact]
    public void Validate_StrongPassword_Passes()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings =>
        {
            settings.RequireLowercase = true;
            settings.RequireUppercase = true;
            settings.RequireDigit = true;
            settings.RequireNonAlphanumeric = true;
            settings.RequiredUniqueChars = 4;
        }));

        var result = validator.TestValidate(new PasswordModel { Password = "Str0ng!Pass" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPassword_FailsWithRequiredMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings());

        var result = validator.TestValidate(new PasswordModel { Password = string.Empty });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Auth.PasswordLabel is required");
    }

    [Fact]
    public void Validate_PasswordShorterThanRequiredLength_FailsWithMinLengthMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings());

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefg" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Auth.PasswordLabel must be at least 8 characters");
    }

    [Fact]
    public void Validate_PasswordAtRequiredLength_PassesMinLength()
    {
        var validator = CreateValidator(CreatePasswordSettings());

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefgh" });

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_PasswordLongerThan100Chars_FailsWithMaxLengthMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings());

        var result = validator.TestValidate(new PasswordModel { Password = new string('a', 101) });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Auth.PasswordLabel must not exceed 100 characters");
    }

    [Fact]
    public void Validate_PasswordAt100Chars_PassesMaxLength()
    {
        var validator = CreateValidator(CreatePasswordSettings());

        var result = validator.TestValidate(new PasswordModel { Password = new string('a', 100) });

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_MissingLowercase_FailsWithSingleRequirementMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequireLowercase = true));

        var result = validator.TestValidate(new PasswordModel { Password = "ABCDEFGH" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter");
    }

    [Fact]
    public void Validate_MissingUppercase_FailsWithSingleRequirementMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequireUppercase = true));

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefgh" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter");
    }

    [Fact]
    public void Validate_MissingDigit_FailsWithSingleRequirementMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequireDigit = true));

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefgh" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit");
    }

    [Fact]
    public void Validate_MissingNonAlphanumeric_FailsWithSingleRequirementMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequireNonAlphanumeric = true));

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefg1" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one non-alphanumeric character");
    }

    [Fact]
    public void Validate_TooFewUniqueChars_FailsWithSingleRequirementMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequiredUniqueChars = 4));

        var result = validator.TestValidate(new PasswordModel { Password = "abababab" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least 4 unique characters");
    }

    [Fact]
    public void Validate_RequiredUniqueCharsOfOne_IsNotEnforced()
    {
        // The unique-chars check only kicks in for values greater than 1.
        var validator = CreateValidator(CreatePasswordSettings(settings => settings.RequiredUniqueChars = 1));

        var result = validator.TestValidate(new PasswordModel { Password = "aaaaaaaa" });

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_MultipleRequirementsViolated_ComposesMessageWithAnd()
    {
        var validator = CreateValidator(CreatePasswordSettings(settings =>
        {
            settings.RequireLowercase = true;
            settings.RequireUppercase = true;
            settings.RequireDigit = true;
            settings.RequireNonAlphanumeric = true;
        }));

        // The composed message lists every configured requirement, not just the violated ones.
        var result = validator.TestValidate(new PasswordModel { Password = "abcdefgh" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter, one uppercase letter, one digit, and one non-alphanumeric character");
    }

    [Fact]
    public void Validate_WithLocalizer_UsesLocalizedRequiredMessage()
    {
        var validator = CreateValidator(CreatePasswordSettings(), new EchoStringLocalizer());

        var result = validator.TestValidate(new PasswordModel { Password = string.Empty });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Validation.PasswordRequired");
    }

    [Fact]
    public void Validate_WithLocalizer_ComposesLocalizedComplexityMessage()
    {
        var validator = CreateValidator(
            CreatePasswordSettings(settings =>
            {
                settings.RequireUppercase = true;
                settings.RequireDigit = true;
            }),
            new EchoStringLocalizer());

        var result = validator.TestValidate(new PasswordModel { Password = "abcdefgh" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Validation.PasswordUppercase; Validation.PasswordDigit");
    }

    [Fact]
    public void WithStrongPasswordRules_NullSecuritySettings_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new PasswordModelValidator(null!, "Auth.PasswordLabel", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithStrongPasswordRules_MissingFieldKey_Throws(string fieldKey)
    {
        var securitySettings = MicrosoftOptions.Create(new SecuritySettings { PasswordSettings = CreatePasswordSettings() });

        Should.Throw<ArgumentException>(() => new PasswordModelValidator(securitySettings, fieldKey, null));
    }

    private static PasswordModelValidator CreateValidator(PasswordSettings passwordSettings, IStringLocalizer? localizer = null)
    {
        var securitySettings = MicrosoftOptions.Create(new SecuritySettings { PasswordSettings = passwordSettings });
        return new PasswordModelValidator(securitySettings, "Auth.PasswordLabel", localizer);
    }

    private static PasswordSettings CreatePasswordSettings(Action<PasswordSettings>? configure = null)
    {
        var settings = new PasswordSettings
        {
            RequiredLength = 8,
            RequiredUniqueChars = 1,
        };
        configure?.Invoke(settings);
        return settings;
    }

    private sealed class PasswordModel
    {
        public string Password { get; set; } = string.Empty;
    }

    private sealed class PasswordModelValidator : AbstractValidator<PasswordModel>
    {
        public PasswordModelValidator(IOptions<SecuritySettings> securitySettings, string fieldKey, IStringLocalizer? localizer)
        {
            this.RuleFor(x => x.Password).WithStrongPasswordRules(securitySettings, fieldKey, localizer: localizer);
        }
    }

    /// <summary>
    /// Localizer stub that echoes the lookup key back as the localized value, making the chosen
    /// translation key observable in the produced validation message.
    /// </summary>
    private sealed class EchoStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }
}
