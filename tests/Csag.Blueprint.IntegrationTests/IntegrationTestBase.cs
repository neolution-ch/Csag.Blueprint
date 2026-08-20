namespace Csag.Blueprint.IntegrationTests;

using System.Net.Http.Headers;

/// <summary>
/// Base class for integration tests: restores the database snapshot before each test (via
/// <see cref="Csag.Blueprint.Testing.Integration.IntegrationTestBase{TFixture}"/>) and fixes the
/// fixture type to <see cref="AppFixture"/> so test classes only take the one constructor argument.
/// </summary>
/// <param name="app">The shared application fixture injected by xUnit.</param>
public abstract class IntegrationTestBase(AppFixture app)
    : Csag.Blueprint.Testing.Integration.IntegrationTestBase<AppFixture>(app)
{
    /// <summary>
    /// Creates a fresh anonymous client that requests the given culture via the Accept-Language
    /// header, for localization assertions.
    /// </summary>
    /// <param name="culture">The requested UI culture (e.g. "de").</param>
    /// <returns>A client configured for culture-sensitive requests.</returns>
    protected HttpClient CreateClientForCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        var client = this.App.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
        return client;
    }
}
