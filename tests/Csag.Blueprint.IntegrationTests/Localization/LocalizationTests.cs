namespace Csag.Blueprint.IntegrationTests.Localization;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Infrastructure.Localization;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.TestHost.Endpoints.Localization.Greeting;
using Csag.Blueprint.Testing.Extensions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for the database-backed localization pipeline, observed through the host's
/// anonymous greeting endpoint. Pins the translation fallback chain (requested-language row →
/// default-language row → code default), the request-culture provider chain (session
/// preferred-language claim → Accept-Language header → configured default), and the translation
/// cache behavior around direct database changes and explicit invalidation.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class LocalizationTests(AppFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// Key of the greeting seeded with database rows in both supported languages. Mirrors the
    /// host's internal translation key catalog.
    /// </summary>
    private const string GreetingHelloKey = "Greeting.Hello";

    private static readonly Uri GreetingUri = new("/api/localization/greeting", UriKind.Relative);

    [Theory]
    [InlineData("de", "Hallo aus der Datenbank")]
    [InlineData("en", "Hello from the database")]
    public async Task Greeting_ResolvesSeededTranslation_PerAcceptLanguage(string culture, string expectedHello)
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = this.CreateClientForCulture(culture);
        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe(culture);
        greeting.Hello.ShouldBe(expectedHello);
    }

    [Fact]
    public async Task Greeting_InGerman_PinsFallbackChain_RequestedThenDefaultThenCode()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = this.CreateClientForCulture("de");
        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe("de");

        // Tier 1: a row in the requested language wins over everything else.
        greeting.Hello.ShouldBe("Hallo aus der Datenbank");

        // Tier 2: a key without a German row falls back to the default-language row, not to the
        // code default ("English-only (code default)").
        greeting.EnglishOnly.ShouldBe("This value exists only in English");

        // Tier 3: a key with no database rows at all resolves to the code-defined default.
        greeting.CodeOnly.ShouldBe("Code-only greeting");
    }

    [Fact]
    public async Task Greeting_InDefaultLanguage_UsesDbRowOverCodeDefault()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = this.CreateClientForCulture("en");
        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe("en");

        // A default-language row beats the code default ("Hello (code default)").
        greeting.Hello.ShouldBe("Hello from the database");
        greeting.EnglishOnly.ShouldBe("This value exists only in English");
        greeting.CodeOnly.ShouldBe("Code-only greeting");
    }

    [Fact]
    public async Task Greeting_FallsBackToDefaultCulture_WhenNoHeaderAndNoClaim()
    {
        var ct = TestContext.Current.CancellationToken;

        // A fresh client sends neither a session cookie nor an Accept-Language header.
        using var client = this.App.CreateClient();
        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe("en");
        greeting.Hello.ShouldBe("Hello from the database");
    }

    [Fact]
    public async Task Greeting_SessionClaimOverridesAcceptLanguage()
    {
        var ct = TestContext.Current.CancellationToken;

        // Give the user a preferred language before signing in; the login endpoint stamps it into
        // the session principal as a claim.
        using (var scope = this.App.CreateDbContextScope())
        {
            var user = await scope.Context.Users.SingleAsync(u => u.Id == SeedData.ViewerAUserId, ct);
            user.PreferredLanguage = "de";
            await scope.Context.SaveChangesAsync(ct);
        }

        using var client = this.App.CreateClient();
        var (loginRsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = SeedData.ViewerAEmail,
                Password = SeedData.DefaultPassword,
            });
        await loginRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        // The header asks for English, but the session claim must win.
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe("de");
        greeting.Hello.ShouldBe("Hallo aus der Datenbank");
    }

    [Fact]
    public async Task Greeting_UsesAcceptLanguage_WhenSessionHasNoLanguageClaim()
    {
        var ct = TestContext.Current.CancellationToken;

        // ManagerA has no preferred language, so the authenticated session carries no language
        // claim and the header decides the culture.
        using var client = this.App.CreateClient();
        var (loginRsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = SeedData.ManagerAEmail,
                Password = SeedData.DefaultPassword,
            });
        await loginRsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("de"));

        var greeting = await GetGreetingAsync(client, ct);

        greeting.Culture.ShouldBe("de");
        greeting.Hello.ShouldBe("Hallo aus der Datenbank");
    }

    [Fact]
    public async Task Greeting_ServesCachedTranslation_UntilExplicitlyInvalidated()
    {
        var ct = TestContext.Current.CancellationToken;
        const string updatedValue = "Direkt in der Datenbank geänderter Wert";

        using var client = this.CreateClientForCulture("de");
        var invalidator = this.App.Services.GetRequiredService<ITranslationCacheInvalidator>();

        try
        {
            // Prime the translation caches with the seeded value.
            var initial = await GetGreetingAsync(client, ct);
            initial.Hello.ShouldBe("Hallo aus der Datenbank");

            // Change the row directly in the database, bypassing the application's write path
            // (which would invalidate the caches itself).
            using (var scope = this.App.CreateDbContextScope())
            {
                var row = await scope.Context.Translations.SingleAsync(
                    t => t.Key == GreetingHelloKey && t.LanguageCode == "de", ct);
                row.Value = updatedValue;
                await scope.Context.SaveChangesAsync(ct);
            }

            // The caches have not been told, so the old value is still served.
            var stale = await GetGreetingAsync(client, ct);
            stale.Hello.ShouldBe("Hallo aus der Datenbank");

            // After invalidation, the next request reloads from the database.
            await invalidator.InvalidateAsync("de", ct);

            var fresh = await GetGreetingAsync(client, ct);
            fresh.Hello.ShouldBe(updatedValue);
        }
        finally
        {
            // The database snapshot restore between tests does not touch the translation caches,
            // so evict the mutated snapshot to keep other tests seeing the seeded values.
            await invalidator.InvalidateAsync("de", ct);
        }
    }

    /// <summary>
    /// Fetches the greeting endpoint with the given client and returns the deserialized response,
    /// asserting a 200 status.
    /// </summary>
    private static async Task<GreetingResponse> GetGreetingAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetAsync(GreetingUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var greeting = await response.Content.ReadFromJsonAsync<GreetingResponse>(BlueprintJsonOptions.Default, ct);
        greeting.ShouldNotBeNull();
        return greeting;
    }
}
