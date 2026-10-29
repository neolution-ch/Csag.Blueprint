namespace Csag.Blueprint.Infrastructure.UnitTests.ServiceAccounts;

using Csag.Blueprint.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for the two selector schemes and the bearer-token rule they share: each request is forwarded to exactly
/// one scheme, picked from the request's own credential.
/// </summary>
public sealed class AuthenticationSchemeSelectorTests
{
    [Theory]
    [InlineData("Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("bearer abc", "abc")]
    [InlineData("BEARER   abc  ", "abc")]
    public void TryGetBearerToken_ReadsTheTokenOfABearerAuthorizationHeader(string authorization, string expected)
    {
        var request = RequestWith(authorization);

        request.TryGetBearerToken(out var token).ShouldBeTrue();
        token.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer    ")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearerabc")]
    public void TryGetBearerToken_FindsNoTokenInAnyOtherHeader(string? authorization)
    {
        var request = RequestWith(authorization);

        request.TryGetBearerToken(out var token).ShouldBeFalse();
        token.ShouldBeNull();
    }

    [Fact]
    public void SessionOrBearer_ForwardsABearerRequestToTheBearerSelector()
    {
        using var services = ServiceAccountTestServices.Build();
        var selector = services.GetPolicySchemeOptions(BlueprintAuthenticationSchemes.SessionOrBearer).ForwardDefaultSelector.ShouldNotBeNull();

        selector(ContextWith(services, "Bearer token")).ShouldBe(BlueprintAuthenticationSchemes.Bearer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer ")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public void SessionOrBearer_ForwardsEveryOtherRequestToTheSessionCookie(string? authorization)
    {
        using var services = ServiceAccountTestServices.Build();
        var selector = services.GetPolicySchemeOptions(BlueprintAuthenticationSchemes.SessionOrBearer).ForwardDefaultSelector.ShouldNotBeNull();

        selector(ContextWith(services, authorization)).ShouldBe(IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public void Bearer_ForwardsToTheServiceAccountSchemeWhenNoOtherKindClaimsTheToken()
    {
        using var services = ServiceAccountTestServices.Build(after: s => s.Configure<BlueprintBearerOptions>(o =>
            o.AddTokenKind("UserToken", token => token.StartsWith("user.", StringComparison.Ordinal))));
        var selector = services.GetPolicySchemeOptions(BlueprintAuthenticationSchemes.Bearer).ForwardDefaultSelector.ShouldNotBeNull();

        selector(ContextWith(services, "Bearer service.token")).ShouldBe(BlueprintAuthenticationSchemes.ServiceAccountToken);
        selector(ContextWith(services, authorization: null)).ShouldBe(BlueprintAuthenticationSchemes.ServiceAccountToken);
    }

    [Fact]
    public void Bearer_ForwardsToTheFirstKindThatClaimsTheToken()
    {
        using var services = ServiceAccountTestServices.Build(after: s => s.Configure<BlueprintBearerOptions>(o => o
            .AddTokenKind("UserToken", token => token.StartsWith("user.", StringComparison.Ordinal))
            .AddTokenKind("AnyToken", _ => true)));
        var selector = services.GetPolicySchemeOptions(BlueprintAuthenticationSchemes.Bearer).ForwardDefaultSelector.ShouldNotBeNull();

        selector(ContextWith(services, "Bearer user.token")).ShouldBe("UserToken");
        selector(ContextWith(services, "Bearer other.token")).ShouldBe("AnyToken");
    }

    private static HttpRequest RequestWith(string? authorization)
    {
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        return context.Request;
    }

    private static DefaultHttpContext ContextWith(IServiceProvider services, string? authorization)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        return context;
    }
}
