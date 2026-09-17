namespace Csag.Blueprint.Web.UnitTests.Services;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="SessionClaimRequestCultureProvider"/> — the three-step resolution order:
/// the session's <see cref="IdentityClaimTypes.PreferredLanguage"/> claim (validated against the
/// supported UI cultures), then the Accept-Language header by descending quality, then null so the
/// configured default culture takes effect.
/// </summary>
public sealed class SessionClaimRequestCultureProviderTests
{
    [Fact]
    public async Task DetermineProviderCultureResult_SupportedClaim_ReturnsClaimCultureAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(preferredLanguageClaim: "de-CH");

        var result = await provider.DetermineProviderCultureResult(context);

        var providerResult = result.ShouldNotBeNull();
        providerResult.Cultures.Single().Value.ShouldBe("de-CH");
        providerResult.UICultures.Single().Value.ShouldBe("de-CH");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_ClaimCasing_NormalizesToSupportedNameAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(preferredLanguageClaim: "DE-CH");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("de-CH");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_LanguageOnlyClaim_MapsToRegionQualifiedCultureAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(preferredLanguageClaim: "de");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("de-CH");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_ClaimWinsOverAcceptLanguageHeaderAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(preferredLanguageClaim: "en-US", acceptLanguage: "de-CH");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("en-US");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_UnsupportedClaim_FallsBackToAcceptLanguageAsync()
    {
        // An unsupported claim value (e.g. stale after the supported set shrank) must not win; the
        // header is the next candidate.
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(preferredLanguageClaim: "fr-FR", acceptLanguage: "en-US");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("en-US");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_NoClaim_MatchesAcceptLanguageHeaderAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(acceptLanguage: "de-CH");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("de-CH");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_AcceptLanguage_HighestQualityWinsAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(acceptLanguage: "de-CH;q=0.5, en-US;q=0.9");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("en-US");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_AcceptLanguage_LanguageOnlyValueFallsBackAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(acceptLanguage: "de");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldNotBeNull().Cultures.Single().Value.ShouldBe("de-CH");
    }

    [Fact]
    public async Task DetermineProviderCultureResult_NothingMatches_ReturnsNullAsync()
    {
        // Null lets the built-in RequestLocalizationOptions default culture take effect.
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext(acceptLanguage: "fr-FR");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DetermineProviderCultureResult_NoClaimAndNoHeader_ReturnsNullAsync()
    {
        var provider = CreateProvider("de-CH", "en-US");
        var context = CreateContext();

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DetermineProviderCultureResult_NoOptions_ReturnsNullAsync()
    {
        // Without the owning RequestLocalizationOptions there is no supported-culture set to validate
        // against, so neither the claim nor the header can produce a result.
        var provider = new SessionClaimRequestCultureProvider();
        var context = CreateContext(preferredLanguageClaim: "de-CH", acceptLanguage: "de-CH");

        var result = await provider.DetermineProviderCultureResult(context);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DetermineProviderCultureResult_NullContext_ThrowsAsync()
    {
        var provider = CreateProvider("de-CH");

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await provider.DetermineProviderCultureResult(null!));
    }

    private static SessionClaimRequestCultureProvider CreateProvider(params string[] supportedUiCultures) => new()
    {
        Options = new RequestLocalizationOptions().AddSupportedUICultures(supportedUiCultures),
    };

    private static DefaultHttpContext CreateContext(string? preferredLanguageClaim = null, string? acceptLanguage = null)
    {
        var context = new DefaultHttpContext();

        if (preferredLanguageClaim is not null)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(IdentityClaimTypes.PreferredLanguage, preferredLanguageClaim) },
                authenticationType: "Test");
            context.User = new ClaimsPrincipal(identity);
        }

        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        return context;
    }
}
