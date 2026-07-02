namespace Csag.Blueprint.Web.Validation;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Password;
using FluentValidation;

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for password field validation using configured security settings.
/// Provides a reusable, chainable rule builder for password fields.
/// </summary>
public static class PasswordRuleBuilderExtensions
{
    private const int MaxPasswordLength = 100;

    /// <summary>
    /// Translation key path for password required validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordRequired</c>.
    /// </summary>
    private const string PasswordRequiredKey = "Validation.PasswordRequired";

    /// <summary>
    /// Translation key path for password minimum length validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordMinLength</c>.
    /// </summary>
    private const string PasswordMinLengthKey = "Validation.PasswordMinLength";

    /// <summary>
    /// Translation key path for max length validation.
    /// Corresponds to <c>TranslationKeys.Validation.MaxLength</c>.
    /// </summary>
    private const string MaxLengthKey = "Validation.MaxLength";

    /// <summary>
    /// Translation key path for password uppercase validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordUppercase</c>.
    /// </summary>
    private const string PasswordUppercaseKey = "Validation.PasswordUppercase";

    /// <summary>
    /// Translation key path for password lowercase validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordLowercase</c>.
    /// </summary>
    private const string PasswordLowercaseKey = "Validation.PasswordLowercase";

    /// <summary>
    /// Translation key path for password digit validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordDigit</c>.
    /// </summary>
    private const string PasswordDigitKey = "Validation.PasswordDigit";

    /// <summary>
    /// Translation key path for password special character validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordSpecialChar</c>.
    /// </summary>
    private const string PasswordSpecialCharKey = "Validation.PasswordSpecialChar";

    /// <summary>
    /// Translation key path for password unique characters validation.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordUniqueChars</c>.
    /// </summary>
    private const string PasswordUniqueCharsKey = "Validation.PasswordUniqueChars";

    /// <summary>
    /// Translation key path for generic password complexity fallback.
    /// Corresponds to <c>TranslationKeys.Validation.PasswordComplexityRequired</c>.
    /// </summary>
    private const string PasswordComplexityRequiredKey = "Validation.PasswordComplexityRequired";

    /// <summary>
    /// Applies strong password rules based on the configured security settings.
    /// Returns IRuleBuilder to allow continued rule chaining.
    /// Uses <see cref="IStringLocalizer"/> for localized validation messages when provided.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the password field.</param>
    /// <param name="securitySettings">The security settings containing password policy.</param>
    /// <param name="fieldKey">The translation key path for the password field label (e.g. <c>TranslationKeys.Auth.Login.PasswordLabel</c>). Used as the <c>IStringLocalizer</c> lookup key when <paramref name="fieldLabelFactory"/> is not supplied.</param>
    /// <param name="fieldLabelFactory">Optional per-request factory for a localized display label.</param>
    /// <param name="localizer">Optional string localizer for translated validation messages.</param>
    /// <returns>The rule builder for continued chaining.</returns>
    public static IRuleBuilder<T, string> WithStrongPasswordRules<T>(
        this IRuleBuilder<T, string> rule,
        IOptions<SecuritySettings> securitySettings,
        string fieldKey,
        Func<string>? fieldLabelFactory = null,
        IStringLocalizer? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(securitySettings);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);

        var passwordSettings = securitySettings.Value.PasswordSettings;

        rule
            .NotEmpty()
            .WithMessage(_ => localizer != null
                ? localizer[PasswordRequiredKey]
                : $"{fieldKey} is required")
            .MinimumLength(passwordSettings.RequiredLength)
            .WithMessage(_ => localizer != null
                ? localizer[PasswordMinLengthKey, new { Min = passwordSettings.RequiredLength }]
                : $"{fieldKey} must be at least {passwordSettings.RequiredLength} characters")
            .MaximumLength(MaxPasswordLength)
            .WithMessage(_ => localizer != null
                ? localizer[MaxLengthKey, new { FieldName = fieldLabelFactory?.Invoke() ?? localizer[fieldKey], Max = MaxPasswordLength }]
                : $"{fieldKey} must not exceed {MaxPasswordLength} characters")
            .Must(password => ValidatePasswordComplexity(password, passwordSettings))
            .WithMessage(_ => localizer != null
                ? GetLocalizedPasswordComplexityMessage(passwordSettings, localizer)
                : GetPasswordComplexityMessage(passwordSettings));

        return rule;
    }

    private static bool ValidatePasswordComplexity(string password, PasswordSettings settings)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (settings.RequireLowercase && !password.Any(char.IsLower))
        {
            return false;
        }

        if (settings.RequireUppercase && !password.Any(char.IsUpper))
        {
            return false;
        }

        if (settings.RequireDigit && !password.Any(char.IsDigit))
        {
            return false;
        }

        if (settings.RequireNonAlphanumeric && !password.Any(c => !char.IsLetterOrDigit(c)))
        {
            return false;
        }

        if (settings.RequiredUniqueChars > 1 && password.Distinct().Count() < settings.RequiredUniqueChars)
        {
            return false;
        }

        return true;
    }

    private static string GetLocalizedPasswordComplexityMessage(PasswordSettings settings, IStringLocalizer localizer)
    {
        var requirements = new List<string>();

        if (settings.RequireLowercase)
        {
            requirements.Add(localizer[PasswordLowercaseKey]);
        }

        if (settings.RequireUppercase)
        {
            requirements.Add(localizer[PasswordUppercaseKey]);
        }

        if (settings.RequireDigit)
        {
            requirements.Add(localizer[PasswordDigitKey]);
        }

        if (settings.RequireNonAlphanumeric)
        {
            requirements.Add(localizer[PasswordSpecialCharKey]);
        }

        if (settings.RequiredUniqueChars > 1)
        {
            requirements.Add(localizer[PasswordUniqueCharsKey, new { Count = settings.RequiredUniqueChars }]);
        }

        return requirements.Count switch
        {
            0 => localizer[PasswordComplexityRequiredKey],
            1 => requirements[0],
            _ => string.Join("; ", requirements),
        };
    }

    private static string GetPasswordComplexityMessage(PasswordSettings settings)
    {
        var requirements = new List<string>();

        if (settings.RequireLowercase)
        {
            requirements.Add("one lowercase letter");
        }

        if (settings.RequireUppercase)
        {
            requirements.Add("one uppercase letter");
        }

        if (settings.RequireDigit)
        {
            requirements.Add("one digit");
        }

        if (settings.RequireNonAlphanumeric)
        {
            requirements.Add("one non-alphanumeric character");
        }

        if (settings.RequiredUniqueChars > 1)
        {
            requirements.Add($"{settings.RequiredUniqueChars} unique characters");
        }

        return requirements.Count switch
        {
            0 => "Password must meet complexity requirements",
            1 => $"Password must contain at least {requirements[0]}",
            _ => $"Password must contain at least {string.Join(", ", requirements.Take(requirements.Count - 1))}, and {requirements[^1]}",
        };
    }
}
