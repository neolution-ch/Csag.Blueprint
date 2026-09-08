namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Cors;
using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using Csag.Blueprint.Web.Options.Api.Security.Jwt;
using Csag.Blueprint.Web.Options.Api.Security.PasswordReset;
using Csag.Blueprint.Web.Options.Api.Security.RequestLimits;
using Csag.Blueprint.Web.Options.Api.Security.ServiceAccountLockout;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="SecuritySettingsValidator"/>: its own rules (CORS policy dictionary,
/// session expiration, cookie policy) and the cascade into every child validator.
/// </summary>
public sealed class SecuritySettingsValidatorTests
{
    private readonly SecuritySettingsValidator validator = new();

    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        var settings = CreateValidSettings();

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullDefaultCorsPolicy_Passes()
    {
        // No global policy means policies are applied per-endpoint; the existence check is skipped.
        var settings = CreateValidSettings();
        settings.DefaultCorsPolicy = null;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DefaultCorsPolicyNotInDictionary_Fails()
    {
        var settings = CreateValidSettings();
        settings.DefaultCorsPolicy = "Missing";

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.DefaultCorsPolicy)
            .WithErrorMessage("DefaultCorsPolicy 'Missing' must exist in CorsPolicies dictionary");
    }

    [Fact]
    public void Validate_EmptyCorsPolicies_Fails()
    {
        var settings = CreateValidSettings();
        settings.DefaultCorsPolicy = null;
        settings.CorsPolicies = new Dictionary<string, CorsSettings>();

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.CorsPolicies)
            .WithErrorMessage("At least one CORS policy must be defined in Blueprint:Security:CorsPolicies");
    }

    [Fact]
    public void Validate_CorsPolicyNameWithInvalidCharacters_Fails()
    {
        var settings = CreateValidSettings();
        settings.DefaultCorsPolicy = null;
        settings.CorsPolicies["bad name!"] = CreateValidCorsSettings();

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "CORS policy name must contain only letters, numbers, hyphens, and underscores");
    }

    [Fact]
    public void Validate_InvalidCorsPolicyValue_CascadesIntoCorsValidator()
    {
        var settings = CreateValidSettings();
        settings.CorsPolicies["Default"].PreflightMaxAgeSeconds = 0;

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "PreflightMaxAgeSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_InvalidJwt_CascadesIntoJwtValidator()
    {
        var settings = CreateValidSettings();
        settings.Jwt.Issuer = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Jwt.Issuer)
            .WithErrorMessage("JWT issuer must not be empty.");
    }

    [Fact]
    public void Validate_InvalidCsrf_CascadesIntoCsrfValidator()
    {
        var settings = CreateValidSettings();
        settings.Csrf.HeaderName = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Csrf.HeaderName)
            .WithErrorMessage("CSRF header name must not be empty.");
    }

    [Fact]
    public void Validate_InvalidMfa_CascadesIntoMfaValidator()
    {
        var settings = CreateValidSettings();
        settings.Mfa.Enabled = false;
        settings.Mfa.Required = true;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Mfa.Required)
            .WithErrorMessage("Mfa.Required can only be true when Mfa.Enabled is also true.");
    }

    [Fact]
    public void Validate_InvalidHttpsRedirect_CascadesIntoHttpsRedirectValidator()
    {
        var settings = CreateValidSettings();
        settings.HttpsRedirect.Enabled = true;
        settings.HttpsRedirect.RedirectStatusCode = 302;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.HttpsRedirect.RedirectStatusCode);
    }

    [Fact]
    public void Validate_InvalidPasswordSettings_CascadesIntoPasswordValidator()
    {
        var settings = CreateValidSettings();
        settings.PasswordSettings.RequiredLength = 0;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.PasswordSettings.RequiredLength)
            .WithErrorMessage("RequiredLength must be at least 1");
    }

    [Fact]
    public void Validate_InvalidPasswordResetSettings_CascadesIntoPasswordResetValidator()
    {
        var settings = CreateValidSettings();
        settings.PasswordResetSettings.TokenLifetimeMinutes = 0;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.PasswordResetSettings.TokenLifetimeMinutes)
            .WithErrorMessage("TokenLifetimeMinutes must be at least 1");
    }

    [Fact]
    public void Validate_InvalidServiceAccountLockout_CascadesIntoLockoutValidator()
    {
        var settings = CreateValidSettings();
        settings.ServiceAccountLockout.MaxFailedAccessAttempts = 0;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.ServiceAccountLockout.MaxFailedAccessAttempts)
            .WithErrorMessage("MaxFailedAccessAttempts must be at least 1");
    }

    [Fact]
    public void Validate_InvalidRequestLimits_CascadesIntoRequestLimitsValidator()
    {
        var settings = CreateValidSettings();
        settings.RequestLimits.MaxRequestBodySizeMegabytes = 0;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequestLimits.MaxRequestBodySizeMegabytes);
    }

    [Fact]
    public void Validate_InvalidOAuth_CascadesIntoOAuthValidator()
    {
        var settings = CreateValidSettings();
        settings.OAuth.FrontendBaseUrl = "/not-absolute";

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.OAuth.FrontendBaseUrl);
    }

    [Fact]
    public void Validate_NullJwt_Fails()
    {
        var settings = CreateValidSettings();
        settings.Jwt = null!;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Jwt)
            .WithErrorMessage("Jwt cannot be null");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveSessionExpirationHours_Fails(int sessionExpirationHours)
    {
        var settings = CreateValidSettings();
        settings.SessionExpirationHours = sessionExpirationHours;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.SessionExpirationHours)
            .WithErrorMessage("SessionExpirationHours must be greater than 0");
    }

    [Fact]
    public void Validate_UndefinedCookieSecurePolicy_Fails()
    {
        var settings = CreateValidSettings();
        settings.CookieSecurePolicy = (CookieSecurePolicy)99;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.CookieSecurePolicy)
            .WithErrorMessage("CookieSecurePolicy must be a valid value (Always, SameAsRequest, or None).");
    }

    [Fact]
    public void Validate_DefaultCorsPolicySetWithNullCorsPolicies_Throws()
    {
        // Pins current behavior: the DefaultCorsPolicy existence rule dereferences CorsPolicies without
        // a null guard, so a null dictionary combined with a configured default policy throws instead of
        // surfacing the "CorsPolicies cannot be null" validation error.
        var settings = CreateValidSettings();
        settings.CorsPolicies = null!;

        Should.Throw<NullReferenceException>(() => this.validator.TestValidate(settings));
    }

    private static CorsSettings CreateValidCorsSettings()
    {
        return new CorsSettings
        {
            AllowedOrigins = "https://app.example.com",
            AllowCredentials = true,
            PreflightMaxAgeSeconds = 600,
        };
    }

    private static SecuritySettings CreateValidSettings()
    {
        return new SecuritySettings
        {
            DefaultCorsPolicy = "Default",
            CorsPolicies = new Dictionary<string, CorsSettings>
            {
                ["Default"] = CreateValidCorsSettings(),
            },
            PasswordSettings = new()
            {
                RequiredLength = 8,
                RequiredUniqueChars = 4,
            },
            PasswordResetSettings = new PasswordResetSettings { TokenLifetimeMinutes = 60 },
            Jwt = new JwtSettings
            {
                SigningKey = new string('k', 64),
                Issuer = "csag-blueprint",
                Audience = "csag-blueprint-api",
                ExpirationHours = 8,
            },
            ServiceAccountLockout = new ServiceAccountLockoutSettings
            {
                MaxFailedAccessAttempts = 5,
                LockoutDurationMinutes = 15,
            },
            Mfa = new MfaSettings
            {
                Enabled = true,
                Required = false,
                RecoveryCodeCount = 10,
                Issuer = "CSAG Blueprint",
                BypassExternalProviders = new List<string>(),
            },
            Csrf = new CsrfSettings
            {
                Enabled = true,
                HeaderName = "X-CSRF-TOKEN",
                CookieName = ".Blueprint.Antiforgery",
                RequestTokenCookieName = "XSRF-REQUEST-TOKEN",
            },
            RequestLimits = new RequestLimitsSettings
            {
                MaxRequestBodySizeMegabytes = 100,
                MultipartBodyLengthLimitMegabytes = 50,
            },
            CookieSecurePolicy = CookieSecurePolicy.Always,
            SessionExpirationHours = 168,
        };
    }
}
