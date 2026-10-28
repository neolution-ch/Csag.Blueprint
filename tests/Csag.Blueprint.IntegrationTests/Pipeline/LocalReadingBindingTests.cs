namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;
using System.Text.Json;
using Csag.Blueprint.Testing.Extensions;

/// <summary>
/// End-to-end tests for local readings bound from the query string. FastEndpoints binds query, route and
/// form values without System.Text.Json, so these pin that they accept exactly the local readings a JSON
/// body does: <c>yyyy-MM-ddTHH:mm[:ss]</c>, never an offset, <c>Z</c>, a fraction, a culture format or a bare
/// date.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class LocalReadingBindingTests(AppFixture app) : IntegrationTestBase(app)
{
    [Theory]
    [InlineData("2026-10-25T10:00:00")]
    [InlineData("2026-10-25T10:00")]
    public async Task QueryLocalReading_InTheReadingFormat_BindsAsItReadsAsync(string at)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri($"at={at}&until=2026-10-25T18:30"), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("at").GetString().ShouldBe("2026-10-25T10:00:00");
        body.RootElement.GetProperty("until").GetString().ShouldBe("2026-10-25T18:30:00");
        body.RootElement.GetProperty("atKind").GetString().ShouldBe(nameof(DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task QueryOptionalLocalReading_Omitted_BindsNullAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri("at=2026-10-25T10:00"), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("until").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("at=2026-10-25T10:00:00Z")]
    [InlineData("at=2026-10-25T10:00:00%2B02:00")]
    [InlineData("at=2026-10-25T10:00:00.123")]
    [InlineData("at=25.10.2026%2010:00")]
    [InlineData("at=2026-10-25")]
    [InlineData("at=2026-10-25T10:00&until=2026-10-25T18:30:00Z")]
    public async Task QueryLocalReading_InAnotherFormat_Returns400Async(string query)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri(query), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    private static Uri EchoUri(string query) => new($"/api/readings/echo?{query}", UriKind.Relative);
}
