namespace Csag.Blueprint.Web.UnitTests.Extensions;

using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Cors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Unit tests for <see cref="CorsBuilderExtensions.AddConfiguredCors"/>. Each test registers CORS on a
/// real <see cref="WebApplicationBuilder"/>, resolves <see cref="CorsOptions"/> from the built host and
/// asserts the named <see cref="CorsPolicy"/> that the semicolon-delimited settings produced:
/// origin/method/header splitting, wildcard handling, credentials, and the preflight cache toggle.
/// Wildcard misconfigurations are asserted to fail at registration time, before any host is built.
/// </summary>
public sealed class CorsBuilderExtensionsTests
{
    private const string PolicyName = "TestPolicy";

    [Fact]
    public async Task AddConfiguredCors_ExplicitOrigins_AreSplitAndTrimmedAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings
        {
            AllowedOrigins = "https://app.example.com ; https://admin.example.com;;",
        });

        policy.Origins.ShouldBe(new[] { "https://app.example.com", "https://admin.example.com" });
        policy.AllowAnyOrigin.ShouldBeFalse();
    }

    [Fact]
    public async Task AddConfiguredCors_WildcardOnlyOrigin_AllowsAnyOriginAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedOrigins = "*" });

        policy.AllowAnyOrigin.ShouldBeTrue();
    }

    [Fact]
    public void AddConfiguredCors_WildcardAmongOtherOrigins_ThrowsAtRegistration()
    {
        // The wildcard only means "any origin" when it stands alone; mixed with explicit origins it
        // would become the literal origin "*" (which no browser ever sends) while CorsPolicy reports
        // AllowAnyOrigin. Registration rejects the ambiguity outright.
        var securitySettings = new SecuritySettings();
        securitySettings.CorsPolicies[PolicyName] = new CorsSettings
        {
            AllowedOrigins = "*;https://app.example.com",
        };
        var builder = WebApplication.CreateBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.AddConfiguredCors(securitySettings));

        exception.Message.ShouldContain(PolicyName);
        exception.Message.ShouldContain("wildcard");
    }

    [Fact]
    public async Task AddConfiguredCors_NoOrigins_LeavesOriginListEmptyAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedOrigins = null });

        policy.Origins.ShouldBeEmpty();
        policy.AllowAnyOrigin.ShouldBeFalse();
    }

    [Fact]
    public async Task AddConfiguredCors_NoMethods_AllowsAnyMethodAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedMethods = null });

        policy.AllowAnyMethod.ShouldBeTrue();
    }

    [Fact]
    public async Task AddConfiguredCors_ExplicitMethods_AreSplitAndTrimmedAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedMethods = "GET; POST ;DELETE" });

        policy.Methods.ShouldBe(new[] { "GET", "POST", "DELETE" });
        policy.AllowAnyMethod.ShouldBeFalse();
    }

    [Fact]
    public async Task AddConfiguredCors_NoHeaders_AllowsAnyHeaderAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedHeaders = " " });

        policy.AllowAnyHeader.ShouldBeTrue();
    }

    [Fact]
    public async Task AddConfiguredCors_ExplicitHeaders_AreSplitAndTrimmedAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedHeaders = "Content-Type; Authorization" });

        policy.Headers.ShouldBe(new[] { "Content-Type", "Authorization" });
        policy.AllowAnyHeader.ShouldBeFalse();
    }

    [Fact]
    public async Task AddConfiguredCors_ExposedHeaders_AreSplitAndTrimmedAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { ExposedHeaders = "X-Total-Count; X-Correlation-Id" });

        policy.ExposedHeaders.ShouldBe(new[] { "X-Total-Count", "X-Correlation-Id" });
    }

    [Fact]
    public async Task AddConfiguredCors_NoExposedHeaders_LeavesListEmptyAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { ExposedHeaders = null });

        policy.ExposedHeaders.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddConfiguredCors_AllowCredentialsWithExplicitOrigins_SupportsCredentialsAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings
        {
            AllowedOrigins = "https://app.example.com",
            AllowCredentials = true,
        });

        policy.SupportsCredentials.ShouldBeTrue();
    }

    [Fact]
    public async Task AddConfiguredCors_CredentialsDisabled_DoesNotSupportCredentialsAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { AllowedOrigins = "https://app.example.com" });

        policy.SupportsCredentials.ShouldBeFalse();
    }

    [Fact]
    public void AddConfiguredCors_WildcardOriginWithCredentials_ThrowsAtRegistration()
    {
        // The CORS protocol forbids any-origin + credentials. The combination is validated up front,
        // at AddConfiguredCors time, so the misconfiguration cannot lie dormant until CorsOptions is
        // first resolved.
        var securitySettings = new SecuritySettings();
        securitySettings.CorsPolicies[PolicyName] = new CorsSettings
        {
            AllowedOrigins = "*",
            AllowCredentials = true,
        };
        var builder = WebApplication.CreateBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.AddConfiguredCors(securitySettings));

        exception.Message.ShouldContain(PolicyName);
        exception.Message.ShouldContain("AllowCredentials");
    }

    [Fact]
    public async Task AddConfiguredCors_PreflightMaxAge_IsAppliedWhenPositiveAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { PreflightMaxAgeSeconds = 600 });

        policy.PreflightMaxAge.ShouldBe(TimeSpan.FromSeconds(600));
    }

    [Fact]
    public async Task AddConfiguredCors_ZeroPreflightMaxAge_LeavesMaxAgeUnsetAsync()
    {
        var policy = await BuildPolicyAsync(new CorsSettings { PreflightMaxAgeSeconds = 0 });

        policy.PreflightMaxAge.ShouldBeNull();
    }

    [Fact]
    public async Task AddConfiguredCors_MultiplePolicies_AreAllRegisteredAsync()
    {
        var securitySettings = new SecuritySettings();
        securitySettings.CorsPolicies["WebApp"] = new CorsSettings { AllowedOrigins = "https://app.example.com" };
        securitySettings.CorsPolicies["ThirdParty"] = new CorsSettings { AllowedOrigins = "https://partner.example.com" };

        var builder = WebApplication.CreateBuilder();
        builder.AddConfiguredCors(securitySettings);
        await using var app = builder.Build();
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

        corsOptions.GetPolicy("WebApp").ShouldNotBeNull().Origins.ShouldBe(new[] { "https://app.example.com" });
        corsOptions.GetPolicy("ThirdParty").ShouldNotBeNull().Origins.ShouldBe(new[] { "https://partner.example.com" });
    }

    [Fact]
    public void AddConfiguredCors_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => CorsBuilderExtensions.AddConfiguredCors(null!, new SecuritySettings()));
    }

    [Fact]
    public void AddConfiguredCors_NullSettings_Throws()
    {
        var builder = WebApplication.CreateBuilder();

        Should.Throw<ArgumentNullException>(() => builder.AddConfiguredCors(null!));
    }

    private static async Task<CorsPolicy> BuildPolicyAsync(CorsSettings corsSettings)
    {
        var securitySettings = new SecuritySettings();
        securitySettings.CorsPolicies[PolicyName] = corsSettings;

        var builder = WebApplication.CreateBuilder();
        builder.AddConfiguredCors(securitySettings);

        // The policy object survives host disposal; only the resolution needs the built app.
        await using var app = builder.Build();
        return app.Services.GetRequiredService<IOptions<CorsOptions>>().Value.GetPolicy(PolicyName).ShouldNotBeNull();
    }
}
