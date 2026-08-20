namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Security.Claims;
using System.Text.Json;
using Csag.Blueprint.Web.Middleware;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Unit tests for <see cref="CsrfMiddleware"/>.
/// </summary>
public sealed class CsrfMiddlewareTests
{
    private readonly CsrfSettings csrfSettings = new()
    {
        Enabled = true,
        HeaderName = "X-CSRF-TOKEN",
        CookieName = ".AspNetCore.Antiforgery",
        RequestTokenCookieName = "XSRF-TOKEN",
    };

    [Fact]
    public async Task InvokeAsync_CsrfDisabled_SkipsValidation()
    {
        // Arrange
        var disabledSettings = new CsrfSettings { Enabled = false, HeaderName = "X-CSRF-TOKEN", CookieName = ".AspNetCore.Antiforgery", RequestTokenCookieName = "XSRF-TOKEN" };
        var antiforgery = new Mock<IAntiforgery>();
        var nextCalled = false;

        var context = CreateCookieAuthenticatedContext("POST");
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(disabledSettings));

        // Assert
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task InvokeAsync_SafeMethod_SkipsValidation(string method)
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        SetupTokenDistribution(antiforgery);
        var nextCalled = false;

        var context = CreateCookieAuthenticatedContext(method);
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedPost_SkipsValidation()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var nextCalled = false;

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_JwtAuthenticatedPost_SkipsValidation()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var nextCalled = false;

        var identity = new ClaimsIdentity("Bearer");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        context.Request.Method = "POST";

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_CookieAuthenticatedPost_ValidatesToken()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        SetupTokenDistribution(antiforgery);
        var nextCalled = false;

        var context = CreateCookieAuthenticatedContext("POST");
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(context), Times.Once);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_InvalidToken_Returns403(string method)
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Callback(() => throw new AntiforgeryValidationException("Invalid token"));
        var nextCalled = false;

        var context = CreateCookieAuthenticatedContext(method);
        context.Items[CorrelationIdMiddleware.CorrelationIdKey] = "test-correlation-id";

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_ReturnsProblemDetailsWithCorrelationId()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Callback(() => throw new AntiforgeryValidationException("Invalid token"));

        var context = CreateCookieAuthenticatedContext("POST");
        context.Items[CorrelationIdMiddleware.CorrelationIdKey] = "test-correlation-123";

        // Use a real MemoryStream to capture the response body
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert
        context.Response.ContentType.ShouldNotBeNull();
        context.Response.ContentType.ShouldContain("application/problem+json");

        responseBody.Position = 0;
        var json = await JsonDocument.ParseAsync(responseBody, cancellationToken: TestContext.Current.CancellationToken);
        var root = json.RootElement;

        root.GetProperty("status").GetInt32().ShouldBe(403);
        root.GetProperty("title").GetString().ShouldBe("CSRF validation failed");
        root.GetProperty("correlationId").GetString().ShouldBe("test-correlation-123");
        root.GetProperty("detail").GetString()!.ShouldContain("CSRF token is missing or invalid");
    }

    [Fact]
    public async Task InvokeAsync_CookieAuthenticatedGet_CallsNextAndDoesNotValidate()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        SetupTokenDistribution(antiforgery);
        var nextCalled = false;

        var context = CreateCookieAuthenticatedContext("GET");
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert — GET is a safe method, so no validation occurs but next is called
        nextCalled.ShouldBeTrue();
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_DoesNotDistributeToken()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, antiforgery.Object, CreateSecurityOptions(this.csrfSettings));

        // Assert — no token distribution for unauthenticated requests
        antiforgery.Verify(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()), Times.Never);
        antiforgery.Verify(a => a.GetTokens(It.IsAny<HttpContext>()), Times.Never);
    }

    private static DefaultHttpContext CreateCookieAuthenticatedContext(string method)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser@example.com"));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        context.Request.Method = method;

        return context;
    }

    private static IOptions<SecuritySettings> CreateSecurityOptions(CsrfSettings csrf)
    {
        var settings = new SecuritySettings { Csrf = csrf };
        return Options.Create(settings);
    }

    private static void SetupTokenDistribution(Mock<IAntiforgery> antiforgery)
    {
        var tokenSet = new AntiforgeryTokenSet("request-token", "cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokenSet);
        antiforgery.Setup(a => a.GetTokens(It.IsAny<HttpContext>())).Returns(tokenSet);
    }

    private static CsrfMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new CsrfMiddleware(next, Microsoft.Extensions.Logging.Abstractions.NullLogger<CsrfMiddleware>.Instance);
    }
}
