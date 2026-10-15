namespace Csag.Blueprint.Infrastructure.UnitTests.ServiceAccounts;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Infrastructure.ServiceAccounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Unit tests for <c>AddBlueprintServiceAccountAuthentication</c>: the schemes it registers, the fixed settings of the
/// service-account bearer scheme, the startup validation of its options, and that it serves a host with no signing key.
/// </summary>
public sealed class ServiceAccountRegistrationTests
{
    [Fact]
    public async Task Registration_AddsTheServiceAccountSchemeBehindTwoSelectorsAsync()
    {
        using var services = ServiceAccountTestServices.Build();
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetSchemeAsync(BlueprintAuthenticationSchemes.ServiceAccountToken)).ShouldNotBeNull()
            .HandlerType.ShouldBe(typeof(JwtBearerHandler));
        (await schemes.GetSchemeAsync(BlueprintAuthenticationSchemes.Bearer)).ShouldNotBeNull()
            .HandlerType.ShouldBe(typeof(PolicySchemeHandler));
        (await schemes.GetSchemeAsync(BlueprintAuthenticationSchemes.SessionOrBearer)).ShouldNotBeNull()
            .HandlerType.ShouldBe(typeof(PolicySchemeHandler));

        BlueprintAuthenticationSchemes.Bearer.ShouldBe("Bearer");
    }

    [Fact]
    public void BearerScheme_PinsTheAlgorithmIssuerAudienceAndLifetimeWithNoSkew()
    {
        using var services = ServiceAccountTestServices.Build();

        var options = services.GetBearerOptions();
        var parameters = options.TokenValidationParameters;

        parameters.ValidAlgorithms.ShouldBe([SecurityAlgorithms.HmacSha256]);
        parameters.RequireSignedTokens.ShouldBeTrue();
        parameters.ValidateIssuerSigningKey.ShouldBeTrue();
        parameters.ValidateIssuer.ShouldBeTrue();
        parameters.ValidIssuer.ShouldBe(ServiceAccountTestServices.Issuer);
        parameters.ValidateAudience.ShouldBeTrue();
        parameters.ValidAudience.ShouldBe(ServiceAccountTestServices.Audience);
        parameters.ValidateLifetime.ShouldBeTrue();
        parameters.RequireExpirationTime.ShouldBeTrue();
        parameters.ClockSkew.ShouldBe(TimeSpan.Zero);
        parameters.NameClaimType.ShouldBe(ServiceAccountClaimTypes.Name);
    }

    [Fact]
    public void BearerScheme_KeepsRawClaimNamesTheInjectedClockAndNoErrorDetails()
    {
        using var services = ServiceAccountTestServices.Build();

        var options = services.GetBearerOptions();

        options.MapInboundClaims.ShouldBeFalse();
        options.TimeProvider.ShouldBeSameAs(services.GetRequiredService<TimeProvider>());
        options.IncludeErrorDetails.ShouldBeFalse();
        options.SaveToken.ShouldBeFalse();
        options.Events.OnTokenValidated.ShouldNotBeNull();
    }

    [Fact]
    public void BearerScheme_KeepsThePackagesSettingsOverAnApplicationConfigure()
    {
        using var services = ServiceAccountTestServices.Build(after: s => s.Configure<JwtBearerOptions>(
            BlueprintAuthenticationSchemes.ServiceAccountToken,
            options =>
            {
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters { ClockSkew = TimeSpan.FromDays(1) };
            }));

        var options = services.GetBearerOptions();

        options.MapInboundClaims.ShouldBeFalse();
        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.Zero);
        options.TokenValidationParameters.ValidAlgorithms.ShouldBe([SecurityAlgorithms.HmacSha256]);
    }

    [Fact]
    public void WithoutASigningKey_EveryServiceResolvesAndTheOptionsPassValidation()
    {
        // Arrange — the host exporting its OpenAPI document constructs every endpoint, and with them the intents,
        // but has no signing key.
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = null);
        using var scope = services.CreateScope();

        // Act & Assert
        scope.ServiceProvider.GetRequiredService<IServiceAccountCredentials>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IServiceAccountSessionLifecycle>().ShouldNotBeNull();
        services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value.SigningKey.ShouldBeNull();
    }

    [Fact]
    public void WithoutASigningKey_TheBearerSchemeRefusesToBuild()
    {
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = null);

        var exception = Should.Throw<InvalidOperationException>(() => services.GetBearerOptions());

        exception.Message.ShouldContain("signing key");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("   ")]
    [InlineData("   0123456789012345678901234567890   ")]
    public void ASigningKeyShorterThanTheMinimumOnceTrimmed_FailsValidation(string signingKey)
    {
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = signingKey);

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value);

        exception.Message.ShouldContain("SigningKey");
    }

    [Fact]
    public void ASigningKeyOfExactlyTheMinimumLength_PassesValidation()
    {
        var signingKey = new string('k', BlueprintServiceAccountOptions.MinimumSigningKeyLength);
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = signingKey);

        services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value.SigningKey.ShouldBe(signingKey);
        services.GetBearerOptions().TokenValidationParameters.IssuerSigningKey.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(999)]
    public void ATokenLifetimeShorterThanOneSecond_FailsValidation(int milliseconds)
    {
        // Token timestamps and the reported lifetime are whole seconds, so a shorter token could be issued expired.
        using var services = ServiceAccountTestServices.Build(configure: o => o.TokenLifetime = TimeSpan.FromMilliseconds(milliseconds));

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value);

        exception.Message.ShouldContain("TokenLifetime (Blueprint:Security:Jwt:ExpirationHours) must be at least one second");
    }

    [Fact]
    public void ATokenLifetimeOfExactlyOneSecond_PassesValidation()
    {
        using var services = ServiceAccountTestServices.Build(configure: o => o.TokenLifetime = TimeSpan.FromSeconds(1));

        services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value.TokenLifetime.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ATokenLifetimeLongerThanTheReportableSeconds_FailsValidation()
    {
        // The lifetime is reported to clients in whole seconds as an int.
        using var services = ServiceAccountTestServices.Build(configure: o => o.TokenLifetime = TimeSpan.FromSeconds((double)int.MaxValue + 1));

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value);

        exception.Message.ShouldContain("TokenLifetime");
    }

    [Fact]
    public void MissingIssuerAudienceLifetimeAndLockout_FailValidationTogether()
    {
        using var services = ServiceAccountTestServices.Build(configure: o =>
        {
            o.Issuer = " ";
            o.Audience = null;
            o.TokenLifetime = TimeSpan.Zero;
            o.MaxFailedAccessAttempts = 0;
            o.LockoutDuration = TimeSpan.FromMinutes(-1);
        });

        var exception = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value);

        exception.Failures.Count().ShouldBe(5);
    }

    [Fact]
    public void AddTokenKind_RefusesASelectorAsTheValidatingScheme()
    {
        var options = new BlueprintBearerOptions();

        Should.Throw<ArgumentException>(() => options.AddTokenKind(BlueprintAuthenticationSchemes.Bearer, _ => true));
        Should.Throw<ArgumentException>(() => options.AddTokenKind(BlueprintAuthenticationSchemes.SessionOrBearer, _ => true));
        options.TokenKinds.ShouldBeEmpty();
    }
}
