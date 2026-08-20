namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Net;
using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Behavioural tests for the security-header middleware registered by
/// <see cref="SecurityExtensions.UseSecurityHeaders"/> and
/// <see cref="SecurityExtensions.UseServerIdentityHeaderRemoval"/>, plus the
/// <see cref="BlueprintMiddlewareExtensions.UseBlueprintSecurityHeaders"/> composition.
/// The inline middleware relies on <c>Response.OnStarting</c>, which only a real server fires, so each
/// test boots a minimal Kestrel host on a loopback socket and observes the headers over HTTP. The probe
/// endpoint stamps the server-identity headers itself so their removal (or survival) is observable
/// regardless of what the server would emit.
/// </summary>
public sealed class SecurityHeadersTests
{
    [Fact]
    public async Task UseSecurityHeaders_Enabled_AddsAllSecurityHeaders()
    {
        // Arrange
        var settings = new SecuritySettings();
        settings.SecurityHeaders.EnableSecurityHeaders = true;

        // Act
        var response = await RunRequestAsync(settings, app => app.UseSecurityHeaders(settings), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions).ShouldBeTrue("X-Content-Type-Options header should be present");
        contentTypeOptions.FirstOrDefault().ShouldBe("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var frameOptions).ShouldBeTrue("X-Frame-Options header should be present");
        frameOptions.FirstOrDefault().ShouldBe("DENY");

        response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy).ShouldBeTrue("Referrer-Policy header should be present");
        referrerPolicy.FirstOrDefault().ShouldBe("strict-origin-when-cross-origin");

        response.Headers.TryGetValues("Permissions-Policy", out var permissionsPolicy).ShouldBeTrue("Permissions-Policy header should be present");
        permissionsPolicy.FirstOrDefault().ShouldBe("geolocation=(), microphone=(), camera=()");
    }

    [Fact]
    public async Task UseSecurityHeaders_Disabled_AddsNoSecurityHeaders()
    {
        // Arrange
        var settings = new SecuritySettings();
        settings.SecurityHeaders.EnableSecurityHeaders = false;

        // Act
        var response = await RunRequestAsync(settings, app => app.UseSecurityHeaders(settings), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-Content-Type-Options").ShouldBeFalse();
        response.Headers.Contains("X-Frame-Options").ShouldBeFalse();
        response.Headers.Contains("Referrer-Policy").ShouldBeFalse();
        response.Headers.Contains("Permissions-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task UseServerIdentityHeaderRemoval_Enabled_RemovesServerIdentityHeaders()
    {
        // Arrange — RemoveServerIdentityHeaders also switches off Kestrel's automatic Server header
        // via ConfigureKestrelServerOptions, mirroring the production wiring.
        var settings = new SecuritySettings();
        settings.SecurityHeaders.RemoveServerIdentityHeaders = true;

        // Act
        var response = await RunRequestAsync(settings, app => app.UseServerIdentityHeaderRemoval(settings), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Server").ShouldBeFalse("Server header should be removed");
        response.Headers.Contains("X-Powered-By").ShouldBeFalse("X-Powered-By header should be removed");
        response.Headers.Contains("X-AspNet-Version").ShouldBeFalse("X-AspNet-Version header should be removed");
        response.Headers.Contains("X-AspNetMvc-Version").ShouldBeFalse("X-AspNetMvc-Version header should be removed");
    }

    [Fact]
    public async Task UseServerIdentityHeaderRemoval_Disabled_KeepsServerIdentityHeaders()
    {
        // Arrange
        var settings = new SecuritySettings();
        settings.SecurityHeaders.RemoveServerIdentityHeaders = false;

        // Act
        var response = await RunRequestAsync(settings, app => app.UseServerIdentityHeaderRemoval(settings), TestContext.Current.CancellationToken);

        // Assert — the headers stamped by the endpoint survive untouched.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("Server", out var server).ShouldBeTrue();
        server.FirstOrDefault().ShouldBe("TestServerIdentity");
        response.Headers.TryGetValues("X-Powered-By", out var poweredBy).ShouldBeTrue();
        poweredBy.FirstOrDefault().ShouldBe("TestFramework");
    }

    [Fact]
    public async Task UseBlueprintSecurityHeaders_AppliesSecurityHeadersAndRemovesServerIdentity()
    {
        // Arrange — the aggregate composition, with HTTPS redirection and HSTS left disabled so the
        // pipeline works over the plain-HTTP loopback host.
        var settings = new SecuritySettings();
        settings.SecurityHeaders.EnableSecurityHeaders = true;
        settings.SecurityHeaders.RemoveServerIdentityHeaders = true;

        // Act
        var response = await RunRequestAsync(settings, app => app.UseBlueprintSecurityHeaders(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions).ShouldBeTrue();
        contentTypeOptions.FirstOrDefault().ShouldBe("nosniff");
        response.Headers.TryGetValues("X-Frame-Options", out var frameOptions).ShouldBeTrue();
        frameOptions.FirstOrDefault().ShouldBe("DENY");
        response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy).ShouldBeTrue();
        referrerPolicy.FirstOrDefault().ShouldBe("strict-origin-when-cross-origin");
        response.Headers.TryGetValues("Permissions-Policy", out var permissionsPolicy).ShouldBeTrue();
        permissionsPolicy.FirstOrDefault().ShouldBe("geolocation=(), microphone=(), camera=()");
        response.Headers.Contains("Server").ShouldBeFalse("Server header should be removed");
        response.Headers.Contains("X-Powered-By").ShouldBeFalse("X-Powered-By header should be removed");
    }

    private static async Task<HttpResponseMessage> RunRequestAsync(
        SecuritySettings securitySettings,
        Action<WebApplication> configurePipeline,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.ConfigureKestrelServerOptions(securitySettings);
        builder.Services.AddSingleton(Options.Create(securitySettings));

        await using var app = builder.Build();
        configurePipeline(app);

        // Stamp the identity headers explicitly so the removal middleware has something to strip.
        app.MapGet("/probe", (HttpContext context) =>
        {
            context.Response.Headers.Server = "TestServerIdentity";
            context.Response.Headers["X-Powered-By"] = "TestFramework";
            context.Response.Headers["X-AspNet-Version"] = "0.0";
            context.Response.Headers["X-AspNetMvc-Version"] = "0.0";
            return Results.Ok();
        });

        await app.StartAsync(cancellationToken);
        using var client = new HttpClient();
        var response = await client.GetAsync(new Uri($"{app.Urls.First()}/probe"), cancellationToken);
        await app.StopAsync(cancellationToken);
        return response;
    }
}
