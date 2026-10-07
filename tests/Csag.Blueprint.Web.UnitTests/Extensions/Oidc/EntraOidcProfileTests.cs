namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using System.Security.Claims;
using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Unit tests for <see cref="EntraOidcProfile"/>: the audience-derived authority, the short-claim
/// handling (<c>MapInboundClaims</c> off, <c>name</c>/<c>roles</c> claim types), the tenant-aware
/// issuer-validation selection (single-tenant keeps the discovery issuer; multi-tenant installs
/// <see cref="EntraClaimPolicy.ValidateMultiTenantIssuer"/>), and the <c>OnTokenValidated</c> hook
/// that normalizes claims for the shared callback.
/// </summary>
public sealed class EntraOidcProfileTests
{
    private const string Scheme = "entra";
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    private readonly EntraOidcProfile profile = new();

    [Fact]
    public void Configure_SingleTenant_DerivesTenantAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.Authority.ShouldBe($"https://login.microsoftonline.com/{TenantId}/v2.0");
    }

    [Fact]
    public void Configure_MultiTenant_UsesOrganizationsAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenant));

        options.Authority.ShouldBe("https://login.microsoftonline.com/organizations/v2.0");
    }

    [Fact]
    public void Configure_MultiTenantAndPersonal_UsesCommonAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenantAndPersonal));

        options.Authority.ShouldBe("https://login.microsoftonline.com/common/v2.0");
    }

    [Fact]
    public void Configure_ExplicitAuthority_WinsOverAudienceDerivation()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings(MicrosoftEntraSignInAudience.MultiTenant);
        settings.Authority = "https://login.microsoftonline.us/organizations/v2.0";

        this.profile.Configure(options, settings);

        options.Authority.ShouldBe("https://login.microsoftonline.us/organizations/v2.0");
    }

    [Fact]
    public void Configure_UnknownSignInAudienceWithoutAuthority_Throws()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings((MicrosoftEntraSignInAudience)999);

        Should.Throw<InvalidOperationException>(() => this.profile.Configure(options, settings));
    }

    [Fact]
    public void Configure_NormalizesShortClaimHandling()
    {
        // Entra id_tokens use short claim names; the profile turns the inbound map off and points the
        // handler at Entra's "name"/"roles" claims instead.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.MapInboundClaims.ShouldBeFalse();
        options.TokenValidationParameters.NameClaimType.ShouldBe("name");
        options.TokenValidationParameters.RoleClaimType.ShouldBe("roles");
    }

    [Fact]
    public void Configure_SingleTenant_KeepsDiscoveryIssuerValidation()
    {
        // A single-tenant app has exactly one issuer, so the discovery-derived issuer suffices and no
        // custom validator is installed.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.TokenValidationParameters.IssuerValidator.ShouldBeNull();
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Fact]
    public void Configure_MultiTenant_InstallsTenantAwareIssuerValidator()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenant));

        options.TokenValidationParameters.IssuerValidator.ShouldBe((IssuerValidator)EntraClaimPolicy.ValidateMultiTenantIssuer);
    }

    [Fact]
    public void Configure_MultiTenantAndPersonal_InstallsValidatorAndForcesValidationOn()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenantAndPersonal));

        options.TokenValidationParameters.IssuerValidator.ShouldBe((IssuerValidator)EntraClaimPolicy.ValidateMultiTenantIssuer);
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Fact]
    public async Task PostConfigure_OnTokenValidated_NormalizesClaimsForCallbackAsync()
    {
        // The wired event delegates to EntraClaimPolicy.NormalizeClaimsForCallback: the raw "sub" claim
        // is mapped to NameIdentifier and a computed email_verified claim is stamped ("true" here
        // because the audience is single-tenant).
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings(MicrosoftEntraSignInAudience.SingleTenant);
        this.profile.Configure(options, settings);
        this.profile.PostConfigure(Scheme, options, settings);

        var identity = new ClaimsIdentity(
            new[] { new Claim("sub", "user-1"), new Claim("email", "user@example.com") },
            authenticationType: "Test");
        var tokenValidatedContext = new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("TestScheme", displayName: null, typeof(OpenIdConnectHandler)),
            options,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties());

        await options.Events.OnTokenValidated(tokenValidatedContext);

        identity.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe("user-1");
        identity.FindFirst(ClaimTypes.Email)!.Value.ShouldBe("user@example.com");
        identity.FindFirst("email_verified")!.Value.ShouldBe("true");
    }

    [Fact]
    public async Task PostConfigure_ExistingEvents_AreKeptAndSeeTheNormalizedClaimsAsync()
    {
        // The application's own handlers are already on the options when the profile post-configures them,
        // and the profile composes with them instead of replacing them.
        var options = new OpenIdConnectOptions();
        string? nameIdentifierSeenByExistingHandler = null;
        Func<RedirectContext, Task> redirectHandler = _ => Task.CompletedTask;
        options.Events.OnRedirectToIdentityProvider = redirectHandler;
        options.Events.OnTokenValidated = context =>
        {
            nameIdentifierSeenByExistingHandler = context.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Task.CompletedTask;
        };

        var settings = CreateSettings(MicrosoftEntraSignInAudience.SingleTenant);
        this.profile.Configure(options, settings);
        this.profile.PostConfigure(Scheme, options, settings);

        var identity = new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, authenticationType: "Test");
        await options.Events.OnTokenValidated(new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("TestScheme", displayName: null, typeof(OpenIdConnectHandler)),
            options,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties()));

        nameIdentifierSeenByExistingHandler.ShouldBe("user-1");
        options.Events.OnRedirectToIdentityProvider.ShouldBeSameAs(redirectHandler);
    }

    [Fact]
    public void PostConfigure_EventsType_Throws()
    {
        // The handler would resolve its events from DI, so the normalization could not be installed.
        var options = new OpenIdConnectOptions { EventsType = typeof(OpenIdConnectEvents) };
        var settings = CreateSettings(MicrosoftEntraSignInAudience.MultiTenant);
        this.profile.Configure(options, settings);

        var exception = Should.Throw<InvalidOperationException>(() => this.profile.PostConfigure(Scheme, options, settings));

        exception.Message.ShouldContain($"'{Scheme}' sets EventsType");
    }

    [Fact]
    public void PostConfigure_EventsSubclassOverridingTokenValidated_Throws()
    {
        // The handler calls the virtual method, which would never reach the normalization delegate.
        var settings = CreateSettings(MicrosoftEntraSignInAudience.MultiTenant);
        var options = new OpenIdConnectOptions();
        this.profile.Configure(options, settings);
        options.Events = new TokenValidatedOverridingEvents();

        var exception = Should.Throw<InvalidOperationException>(() => this.profile.PostConfigure(Scheme, options, settings));

        exception.Message.ShouldContain($"'{Scheme}' uses {nameof(TokenValidatedOverridingEvents)}, which overrides TokenValidated");
    }

    [Fact]
    public void PostConfigure_EventsSubclassOverridingOtherMethods_IsAccepted()
    {
        var settings = CreateSettings(MicrosoftEntraSignInAudience.MultiTenant);
        var options = new OpenIdConnectOptions();
        this.profile.Configure(options, settings);
        options.Events = new RemoteFailureOverridingEvents();

        Should.NotThrow(() => this.profile.PostConfigure(Scheme, options, settings));
    }

    [Fact]
    public void PostConfigure_InterfaceDefault_LeavesTheOptionsAlone()
    {
        // A profile implementing the interface directly keeps compiling and gets a no-op post-configure step.
        IOidcProviderProfile directImplementation = new ConfigureOnlyProfile();
        var options = new OpenIdConnectOptions();
        Func<TokenValidatedContext, Task> tokenValidated = _ => Task.CompletedTask;
        options.Events.OnTokenValidated = tokenValidated;

        directImplementation.PostConfigure(Scheme, options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenant));

        options.Events.OnTokenValidated.ShouldBeSameAs(tokenValidated);
    }

    private static OidcProviderSettings CreateSettings(MicrosoftEntraSignInAudience audience) => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Entra,
        ClientId = "client-id",
        ClientSecret = "client-secret",
        TenantId = TenantId,
        SignInAudience = audience,
    };

    private sealed class ConfigureOnlyProfile : IOidcProviderProfile
    {
        public void Configure(OpenIdConnectOptions options, OidcProviderSettings settings)
        {
            options.ClientId = settings.ClientId;
        }
    }

    private sealed class TokenValidatedOverridingEvents : OpenIdConnectEvents
    {
        public override Task TokenValidated(TokenValidatedContext context) => Task.CompletedTask;
    }

    private sealed class RemoteFailureOverridingEvents : OpenIdConnectEvents
    {
        public override Task RemoteFailure(RemoteFailureContext context) => Task.CompletedTask;
    }
}
