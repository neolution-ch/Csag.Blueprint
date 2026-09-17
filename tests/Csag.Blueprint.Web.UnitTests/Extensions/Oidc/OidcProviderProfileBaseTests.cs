namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// Unit tests for the shared configuration applied by <see cref="OidcProviderProfileBase"/>
/// (<c>ApplyCommon</c> and <c>ApplyIssuerValidation</c>). The protected helpers are exercised through
/// <see cref="GenericOidcProfile"/>, the thinnest concrete profile, so every assertion here holds for
/// all providers.
/// </summary>
public sealed class OidcProviderProfileBaseTests
{
    private readonly GenericOidcProfile profile = new();

    [Fact]
    public void Configure_AppliesAuthorizationCodePkceFlow()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.ResponseType.ShouldBe(OpenIdConnectResponseType.Code);
        options.UsePkce.ShouldBeTrue();
        options.SaveTokens.ShouldBeFalse();
        options.SignInScheme.ShouldBe(IdentityConstants.ExternalScheme);
    }

    [Fact]
    public void Configure_CopiesClientCredentials()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.ClientId.ShouldBe("client-id");
        options.ClientSecret.ShouldBe("client-secret");
    }

    [Fact]
    public void Configure_DefaultScopes_ReplaceHandlerDefaults()
    {
        // The handler's own defaults ("openid profile") are cleared first, so the configured
        // semicolon-delimited string is the complete scope set.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.Scope.ShouldBe(new[] { "openid", "profile", "email" });
    }

    [Fact]
    public void Configure_ScopesAreTrimmedAndEmptyEntriesDropped()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.Scopes = " openid ;; profile ;custom-scope";

        this.profile.Configure(options, settings);

        options.Scope.ShouldBe(new[] { "openid", "profile", "custom-scope" });
    }

    [Fact]
    public void Configure_DefaultPrompt_ForcesAccountChooser()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.Prompt.ShouldBe("select_account");
    }

    [Fact]
    public void Configure_BlankPrompt_LeavesPromptUnset()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.Prompt = "   ";

        this.profile.Configure(options, settings);

        options.Prompt.ShouldBeNull();
    }

    [Fact]
    public void Configure_MetadataAddress_IsAppliedWhenSet()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.MetadataAddress = "https://idp.example.com/custom/metadata";

        this.profile.Configure(options, settings);

        options.MetadataAddress.ShouldBe("https://idp.example.com/custom/metadata");
    }

    [Fact]
    public void Configure_BlankMetadataAddress_LeavesMetadataAddressUnset()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.MetadataAddress = " ";

        this.profile.Configure(options, settings);

        options.MetadataAddress.ShouldBeNull();
    }

    [Fact]
    public void Configure_CopiesMetadataAndUserInfoToggles()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.RequireHttpsMetadata = false;
        settings.GetClaimsFromUserInfoEndpoint = false;

        this.profile.Configure(options, settings);

        options.RequireHttpsMetadata.ShouldBeFalse();
        options.GetClaimsFromUserInfoEndpoint.ShouldBeFalse();
    }

    [Fact]
    public void Configure_DefaultIssuerValidation_KeepsDiscoveryDerivedIssuer()
    {
        // Default: validation stays on and no explicit issuer list is set, so the handler validates
        // against the single issuer from the discovery document.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidIssuers.ShouldBeNull();
    }

    [Fact]
    public void Configure_ValidateIssuerDisabled_TurnsOffIssuerValidation()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.ValidateIssuer = false;

        this.profile.Configure(options, settings);

        options.TokenValidationParameters.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void Configure_ExplicitValidIssuers_OverrideDiscoveryIssuer()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.ValidIssuers = new List<string> { "https://issuer-a.example.com", "https://issuer-b.example.com" };

        this.profile.Configure(options, settings);

        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidIssuers.ShouldBe(settings.ValidIssuers);
    }

    [Fact]
    public void Configure_EmptyValidIssuersList_IsIgnored()
    {
        // An empty allow-list would reject every issuer; the { Count: > 0 } pattern deliberately treats
        // it like "not configured" and keeps the discovery-derived issuer.
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.ValidIssuers = new List<string>();

        this.profile.Configure(options, settings);

        options.TokenValidationParameters.ValidIssuers.ShouldBeNull();
    }

    private static OidcProviderSettings CreateSettings() => new()
    {
        Enabled = true,
        ClientId = "client-id",
        ClientSecret = "client-secret",
    };
}
