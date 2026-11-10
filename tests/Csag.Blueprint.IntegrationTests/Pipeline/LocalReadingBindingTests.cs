namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;
using System.Text.Json;
using Csag.Blueprint.Testing.Extensions;

/// <summary>
/// End-to-end tests for local readings bound from the query string, headers and form fields. FastEndpoints
/// binds those without System.Text.Json, so these pin that they accept exactly the local readings a JSON
/// body does: <c>yyyy-MM-ddTHH:mm[:ss]</c>, never an offset, <c>Z</c>, a fraction, a culture format, a bare
/// date or an empty value, and that a refused value is a 400 rather than a server error.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class LocalReadingBindingTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri EchoFormUri = new("/api/readings/echo-form", UriKind.Relative);

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
    [InlineData("at=")] // empty required
    public async Task QueryLocalReading_InAnotherFormat_Returns400Async(string query)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri(query), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task QueryOptionalLocalReading_Empty_BindsTheDefaultReadingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // FastEndpoints' UseDefaultValuesForNullableProps (on by default) binds an empty value of a nullable
        // property to the parser's value without reporting its failure, so an empty DateTime? is
        // default(DateTime), never a 400 or a server error.
        using var response = await this.App.AnonymousClient.GetAsync(EchoUri("at=2026-10-25T10:00&until="), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("until").GetString().ShouldBe("0001-01-01T00:00:00");
    }

    [Fact]
    public async Task HeaderLocalReadings_InTheReadingFormat_BindAsTheyReadAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = HeaderRequest("2026-10-25T10:00", "2026-10-25T18:30:00");
        using var response = await this.App.AnonymousClient.SendAsync(request, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("at").GetString().ShouldBe("2026-10-25T10:00:00");
        body.RootElement.GetProperty("until").GetString().ShouldBe("2026-10-25T18:30:00");
    }

    [Theory]
    [InlineData("2026-10-25T10:00:00Z", null)]
    [InlineData("25.10.2026 10:00", null)]
    [InlineData("2026-10-25T10:00", "2026-10-25T18:30:00+01:00")]
    public async Task HeaderLocalReading_InAnotherFormat_Returns400Async(string at, string? until)
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = HeaderRequest(at, until);
        using var response = await this.App.AnonymousClient.SendAsync(request, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task FormLocalReadings_InTheReadingFormat_BindAsTheyReadAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var form = Form("2026-10-25T10:00", until: null);
        using var response = await this.App.AnonymousClient.PostAsync(EchoFormUri, form, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        body.RootElement.GetProperty("at").GetString().ShouldBe("2026-10-25T10:00:00");
        body.RootElement.GetProperty("until").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("2026-10-25T10:00:00.5", null)]
    [InlineData("", null)] // empty required
    [InlineData("2026-10-25T10:00", "2026-10-25")]
    public async Task FormLocalReading_InAnotherFormat_Returns400Async(string at, string? until)
    {
        var ct = TestContext.Current.CancellationToken;

        using var form = Form(at, until);
        using var response = await this.App.AnonymousClient.PostAsync(EchoFormUri, form, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    private static Uri EchoUri(string query) => new($"/api/readings/echo?{query}", UriKind.Relative);

    private static HttpRequestMessage HeaderRequest(string at, string? until)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/readings/echo-header", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("X-At", at);
        if (until is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Until", until);
        }

        return request;
    }

    private static MultipartFormDataContent Form(string at, string? until)
    {
        var form = new MultipartFormDataContent { { new StringContent(at), "at" } };
        if (until is not null)
        {
            form.Add(new StringContent(until), "until");
        }

        return form;
    }
}
