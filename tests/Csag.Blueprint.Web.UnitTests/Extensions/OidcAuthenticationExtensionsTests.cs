namespace Csag.Blueprint.Web.UnitTests.Extensions;

using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// Unit tests for <see cref="OidcAuthenticationExtensions"/>: the callback path each scheme listens on, and how a
/// configured frontend base URL routes the provider round trip through the frontend origin.
/// </summary>
public sealed class OidcAuthenticationExtensionsTests
{
    private const string Scheme = "google";
    private const string CallbackPath = "/api/auth/signin-google";
    private const string ApiHostRedirectUri = "https://api-internal.a.run.app/api/auth/signin-google";

    [Fact]
    public void AddOidcAuthentication_NoCallbackPath_ListensOnTheProxiedDefault()
    {
        using var services = BuildServices(frontendBaseUrl: null, callbackPath: null);

        GetOptions(services).CallbackPath.Value.ShouldBe("/api/auth/signin-oidc/google");
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/portal")]
    public async Task RedirectToIdentityProvider_WithFrontendBaseUrl_ReturnsThroughTheFrontendOrigin(string frontendBaseUrl)
    {
        using var services = BuildServices(frontendBaseUrl);
        var options = GetOptions(services);
        var context = CreateRedirectContext(services, options);

        await options.Events.RedirectToIdentityProvider(context);

        context.ProtocolMessage.RedirectUri.ShouldBe("https://app.example.com/api/auth/signin-google");
    }

    [Fact]
    public async Task RedirectToIdentityProvider_WithoutFrontendBaseUrl_KeepsTheHandlersRedirectUri()
    {
        using var services = BuildServices(frontendBaseUrl: null);
        var options = GetOptions(services);
        var context = CreateRedirectContext(services, options);

        await options.Events.RedirectToIdentityProvider(context);

        context.ProtocolMessage.RedirectUri.ShouldBe(ApiHostRedirectUri);
    }

    [Fact]
    public async Task RedirectToIdentityProvider_EntraProfile_IsStillRoutedAfterTheProfileReplacesItsEvents()
    {
        using var services = BuildServices("https://app.example.com", profile: OidcProviderProfile.Entra);
        var options = GetOptions(services);
        var context = CreateRedirectContext(services, options);

        await options.Events.RedirectToIdentityProvider(context);

        context.ProtocolMessage.RedirectUri.ShouldBe("https://app.example.com/api/auth/signin-google");
    }

    [Fact]
    public async Task RemoteFailure_WithTheChallengesReturnAddress_RedirectsThere()
    {
        using var services = BuildServices("https://app.example.com");
        var options = GetOptions(services);
        var context = CreateRemoteFailureContext(services, options, new AuthenticationProperties
        {
            RedirectUri = "/api/auth/external/callback?returnUrl=%2Finvite",
        });

        await options.Events.RemoteFailure(context);

        context.Result.ShouldNotBeNull();
        context.Result.Handled.ShouldBeTrue();
        context.Response.Headers.Location.ToString().ShouldBe("/api/auth/external/callback?returnUrl=%2Finvite");
    }

    [Fact]
    public async Task RemoteFailure_ClearsTheExternalCookie()
    {
        // An earlier attempt's external identity must not be judged in place of this failure.
        using var services = BuildServices("https://app.example.com");
        var options = GetOptions(services);
        var context = CreateRemoteFailureContext(services, options, new AuthenticationProperties
        {
            RedirectUri = "/api/auth/external/callback?returnUrl=%2F",
        });

        await options.Events.RemoteFailure(context);

        var cleared = context.Response.Headers.SetCookie.ToString();
        cleared.ShouldContain($".AspNetCore.{IdentityConstants.ExternalScheme}=;");
        cleared.ShouldContain("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public async Task RemoteFailure_ApplicationHandlerConfiguredAfterRegistration_RunsFirstAndIsStillFollowedByTheRedirect()
    {
        var applicationHandlerRan = false;
        using var services = BuildServices(
            "https://app.example.com",
            configureApplication: collection => collection.Configure<OpenIdConnectOptions>(Scheme, options =>
                options.Events.OnRemoteFailure = _ =>
                {
                    applicationHandlerRan = true;
                    return Task.CompletedTask;
                }));
        var options = GetOptions(services);
        var context = CreateRemoteFailureContext(services, options, new AuthenticationProperties
        {
            RedirectUri = "/api/auth/external/callback?returnUrl=%2F",
        });

        await options.Events.RemoteFailure(context);

        applicationHandlerRan.ShouldBeTrue();
        context.Response.Headers.Location.ToString().ShouldBe("/api/auth/external/callback?returnUrl=%2F");
    }

    [Fact]
    public async Task RemoteFailure_ApplicationHandlerThatHandlesTheFailure_IsNotOverridden()
    {
        using var services = BuildServices(
            "https://app.example.com",
            configureApplication: collection => collection.Configure<OpenIdConnectOptions>(Scheme, options =>
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/handled-by-the-application");
                    context.HandleResponse();
                    return Task.CompletedTask;
                }));
        var options = GetOptions(services);
        var context = CreateRemoteFailureContext(services, options, new AuthenticationProperties
        {
            RedirectUri = "/api/auth/external/callback?returnUrl=%2F",
        });

        await options.Events.RemoteFailure(context);

        context.Response.Headers.Location.ToString().ShouldBe("/handled-by-the-application");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("//evil.example.com/")]
    [InlineData("/\\evil.example.com/")]
    [InlineData("https://evil.example.com/")]
    public async Task RemoteFailure_WithoutALocalReturnAddress_RedirectsToTheFrontendWithAnError(string? returnAddress)
    {
        using var services = BuildServices("https://app.example.com");
        var options = GetOptions(services);
        var properties = returnAddress is null ? null : new AuthenticationProperties { RedirectUri = returnAddress };
        var context = CreateRemoteFailureContext(services, options, properties);

        await options.Events.RemoteFailure(context);

        context.Result.ShouldNotBeNull();
        context.Result.Handled.ShouldBeTrue();
        context.Response.Headers.Location.ToString().ShouldBe("https://app.example.com/?error=external_auth_failed");
    }

    private static ServiceProvider BuildServices(
        string? frontendBaseUrl,
        string? callbackPath = CallbackPath,
        OidcProviderProfile profile = OidcProviderProfile.Google,
        Action<IServiceCollection>? configureApplication = null)
    {
        var securitySettings = new SecuritySettings
        {
            OAuth = new OAuthSettings
            {
                FrontendBaseUrl = frontendBaseUrl,
                Providers =
                {
                    [Scheme] = new OidcProviderSettings
                    {
                        Enabled = true,
                        Profile = profile,
                        ClientId = "client-id",
                        ClientSecret = "0123456789abcdef",
                        CallbackPath = callbackPath,
                    },
                },
            },
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddCookie(IdentityConstants.ExternalScheme);
        services.AddOidcAuthentication(securitySettings);
        configureApplication?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static OpenIdConnectOptions GetOptions(ServiceProvider services)
    {
        return services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(Scheme);
    }

    private static RedirectContext CreateRedirectContext(ServiceProvider services, OpenIdConnectOptions options)
    {
        return new RedirectContext(CreateHttpContext(services), CreateScheme(), options, new AuthenticationProperties())
        {
            ProtocolMessage = new OpenIdConnectMessage { RedirectUri = ApiHostRedirectUri },
        };
    }

    private static RemoteFailureContext CreateRemoteFailureContext(
        ServiceProvider services,
        OpenIdConnectOptions options,
        AuthenticationProperties? properties)
    {
        return new RemoteFailureContext(CreateHttpContext(services), CreateScheme(), options, new AuthenticationFailureException("Correlation failed."))
        {
            Properties = properties,
        };
    }

    private static DefaultHttpContext CreateHttpContext(ServiceProvider services)
    {
        return new DefaultHttpContext { RequestServices = services };
    }

    private static AuthenticationScheme CreateScheme()
    {
        return new AuthenticationScheme(Scheme, displayName: null, typeof(OpenIdConnectHandler));
    }
}
