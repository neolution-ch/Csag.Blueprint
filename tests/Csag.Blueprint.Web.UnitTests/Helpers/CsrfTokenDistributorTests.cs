namespace Csag.Blueprint.Web.UnitTests.Helpers;

using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Moq;

/// <summary>
/// Unit tests for <see cref="CsrfTokenDistributor"/>.
/// </summary>
public sealed class CsrfTokenDistributorTests
{
    private readonly SecuritySettings securitySettings = new()
    {
        Csrf = new CsrfSettings
        {
            Enabled = true,
            HeaderName = "X-CSRF-TOKEN",
            CookieName = ".AspNetCore.Antiforgery",
            RequestTokenCookieName = "XSRF-TOKEN",
        },
        CookieSecurePolicy = CookieSecurePolicy.SameAsRequest,
    };

    [Fact]
    public void DistributeRequestToken_WhenDisabled_DoesNothing()
    {
        // Arrange
        var disabledSettings = new SecuritySettings { Csrf = new CsrfSettings { Enabled = false, HeaderName = "X-CSRF-TOKEN", CookieName = ".AspNetCore.Antiforgery", RequestTokenCookieName = "XSRF-TOKEN" } };
        var antiforgery = new Mock<IAntiforgery>();
        var context = new DefaultHttpContext();

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, disabledSettings);

        // Assert
        antiforgery.Verify(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()), Times.Never);
        antiforgery.Verify(a => a.GetTokens(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public void DistributeRequestToken_NoExistingCookie_CallsGetAndStoreTokens()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var tokenSet = new AntiforgeryTokenSet("request-token-value", "cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokenSet);

        var context = new DefaultHttpContext();

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, this.securitySettings);

        // Assert
        antiforgery.Verify(a => a.GetAndStoreTokens(context), Times.Once);
        antiforgery.Verify(a => a.GetTokens(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public void DistributeRequestToken_ExistingCookie_CallsGetTokens()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var tokenSet = new AntiforgeryTokenSet("request-token-value", null, "form-field", "header-name");
        antiforgery.Setup(a => a.GetTokens(It.IsAny<HttpContext>())).Returns(tokenSet);

        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = ".AspNetCore.Antiforgery=existing-value";

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, this.securitySettings);

        // Assert — valid cookie reused, no need to store new tokens
        antiforgery.Verify(a => a.GetTokens(context), Times.Once);
        antiforgery.Verify(a => a.GetAndStoreTokens(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public void DistributeRequestToken_StaleCookie_FallsBackToGetAndStoreTokens()
    {
        // Arrange — GetTokens returns non-null CookieToken, indicating the existing cookie
        // was undecryptable (e.g. Data Protection keys rotated after container recreate)
        var antiforgery = new Mock<IAntiforgery>();
        var staleTokenSet = new AntiforgeryTokenSet("stale-request-token", "new-cookie-token", "form-field", "header-name");
        var freshTokenSet = new AntiforgeryTokenSet("fresh-request-token", "fresh-cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetTokens(It.IsAny<HttpContext>())).Returns(staleTokenSet);
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(freshTokenSet);

        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = ".AspNetCore.Antiforgery=stale-encrypted-value";

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, this.securitySettings);

        // Assert — detected stale cookie, fell back to GetAndStoreTokens to persist fresh pair
        antiforgery.Verify(a => a.GetTokens(context), Times.Once);
        antiforgery.Verify(a => a.GetAndStoreTokens(context), Times.Once);

        var setCookieHeaders = context.Response.Headers.SetCookie.ToString();
        setCookieHeaders.ShouldContain("XSRF-TOKEN=fresh-request-token");
    }

    [Fact]
    public void DistributeRequestToken_SetsNonHttpOnlyCookieWithCorrectOptions()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var tokenSet = new AntiforgeryTokenSet("the-request-token", "cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokenSet);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, this.securitySettings);

        // Assert — verify the cookie was appended to the actual response
        // DefaultHttpContext uses a real response, so we check SetCookie headers
        var setCookieHeaders = context.Response.Headers.SetCookie.ToString();
        setCookieHeaders.ShouldContain("XSRF-TOKEN=the-request-token");
        setCookieHeaders.ShouldContain("path=/");
        setCookieHeaders.ShouldContain("secure");
        setCookieHeaders.ShouldContain("samesite=lax");
        setCookieHeaders.ShouldNotContain("httponly");
    }

    [Fact]
    public void DistributeRequestToken_HttpRequest_DoesNotSetSecureFlag()
    {
        // Arrange
        var antiforgery = new Mock<IAntiforgery>();
        var tokenSet = new AntiforgeryTokenSet("the-request-token", "cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokenSet);

        var context = new DefaultHttpContext();

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, this.securitySettings);

        // Assert — Secure flag should not be set for HTTP requests with SameAsRequest policy
        var setCookieHeaders = context.Response.Headers.SetCookie.ToString();
        setCookieHeaders.ShouldContain("XSRF-TOKEN=the-request-token");
        setCookieHeaders.ShouldNotContain("secure");
    }

    [Fact]
    public void DistributeRequestToken_AlwaysPolicy_SetsSecureFlagOnHttpRequest()
    {
        // Arrange
        var alwaysSecureSettings = new SecuritySettings
        {
            Csrf = new CsrfSettings
            {
                Enabled = true,
                HeaderName = "X-CSRF-TOKEN",
                CookieName = ".AspNetCore.Antiforgery",
                RequestTokenCookieName = "XSRF-TOKEN",
            },
            CookieSecurePolicy = CookieSecurePolicy.Always,
        };

        var antiforgery = new Mock<IAntiforgery>();
        var tokenSet = new AntiforgeryTokenSet("the-request-token", "cookie-token", "form-field", "header-name");
        antiforgery.Setup(a => a.GetAndStoreTokens(It.IsAny<HttpContext>())).Returns(tokenSet);

        var context = new DefaultHttpContext();

        // Act
        CsrfTokenDistributor.DistributeRequestToken(context, antiforgery.Object, alwaysSecureSettings);

        // Assert — Secure flag should be set even for HTTP requests with Always policy
        var setCookieHeaders = context.Response.Headers.SetCookie.ToString();
        setCookieHeaders.ShouldContain("XSRF-TOKEN=the-request-token");
        setCookieHeaders.ShouldContain("secure");
    }
}
