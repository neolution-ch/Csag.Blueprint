namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Text.Json;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Unit tests for <see cref="SingleCredentialMiddleware"/>: a request with the session cookie and a bearer token is
/// refused with a ProblemDetails 400 before authentication; a request with either one alone passes.
/// </summary>
public sealed class SingleCredentialMiddlewareTests
{
    private const string SessionCookieName = ".AspNetCore.Identity.Application";

    [Fact]
    public async Task InvokeAsync_SessionCookieAndBearerToken_RefusesWithProblemDetailsAsync()
    {
        // Arrange
        var context = CreateContext(cookie: SessionCookieName, authorization: "Bearer abc.def.ghi");
        context.Items[CorrelationIdMiddleware.CorrelationIdKey] = "corr-123";
        var nextCalled = false;

        // Act
        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(SessionCookieName));

        // Assert
        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldNotBeNull().ShouldStartWith("application/problem+json");

        var body = await ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("correlationId").GetString().ShouldBe("corr-123");
        body.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_BearerTokenOnly_PassesAsync()
    {
        var context = CreateContext(cookie: null, authorization: "Bearer abc.def.ghi");
        var nextCalled = false;

        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(SessionCookieName));

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SessionCookieOnly_PassesAsync()
    {
        var context = CreateContext(cookie: SessionCookieName, authorization: null);
        var nextCalled = false;

        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(SessionCookieName));

        nextCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task InvokeAsync_SessionCookieAndAnAuthorizationHeaderWithoutABearerToken_PassesAsync(string authorization)
    {
        // The rule the scheme selectors apply: neither header carries a bearer token, so the cookie alone authenticates.
        var context = CreateContext(cookie: SessionCookieName, authorization: authorization);
        var nextCalled = false;

        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(SessionCookieName));

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_BearerTokenAndAnotherCookie_PassesAsync()
    {
        var context = CreateContext(cookie: "XSRF-TOKEN", authorization: "Bearer abc.def.ghi");
        var nextCalled = false;

        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(SessionCookieName));

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithoutASessionCookieScheme_PassesAsync()
    {
        // A host without cookie sessions has no session cookie name to look for.
        var context = CreateContext(cookie: SessionCookieName, authorization: "Bearer abc.def.ghi");
        var nextCalled = false;

        await CreateMiddleware(() => nextCalled = true).InvokeAsync(context, CookieOptions(cookieName: null));

        nextCalled.ShouldBeTrue();
    }

    private static SingleCredentialMiddleware CreateMiddleware(Action onNext)
        => new(
            _ =>
            {
                onNext();
                return Task.CompletedTask;
            },
            NullLogger<SingleCredentialMiddleware>.Instance);

    private static DefaultHttpContext CreateContext(string? cookie, string? authorization)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (cookie is not null)
        {
            context.Request.Headers.Cookie = $"{cookie}=value";
        }

        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        return context;
    }

    private static IOptionsMonitor<CookieAuthenticationOptions> CookieOptions(string? cookieName)
    {
        // The cookie handler names the cookie when its scheme is registered; unregistered, the name stays unset.
        var options = new CookieAuthenticationOptions();
        if (cookieName is not null)
        {
            options.Cookie.Name = cookieName;
        }

        var monitor = new Mock<IOptionsMonitor<CookieAuthenticationOptions>>();
        monitor.Setup(m => m.Get(IdentityConstants.ApplicationScheme)).Returns(options);
        return monitor.Object;
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}
