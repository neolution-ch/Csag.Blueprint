namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Csag.Blueprint.Testing.Extensions;

/// <summary>
/// End-to-end tests for instants (<see cref="DateTimeOffset"/>) in a JSON body and bound from the query string,
/// headers and form fields. They pin that every source accepts an instant only with a UTC offset or <c>Z</c>,
/// keeps the instant it names, and answers a value without one, which the server would otherwise read in its own
/// zone, with a 400 rather than a server error.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class InstantBindingTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri EchoBodyUri = new("/api/readings/echo-instant", UriKind.Relative);

    private static readonly Uri EchoFormUri = new("/api/readings/echo-instant-form", UriKind.Relative);

    private static readonly DateTimeOffset EightUtc = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-10-25T08:00:00Z")]
    [InlineData("2026-10-25T10:00:00+02:00")]
    [InlineData("2026-10-25T03:00:00-05:00")]
    public async Task BodyInstant_WithOffsetOrZ_KeepsTheInstantAsync(string at)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.PostAsJsonAsync(EchoBodyUri, new { at, until = at }, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Instant(body, "at").ShouldBe(EightUtc);
        Instant(body, "until").ShouldBe(EightUtc);
    }

    [Theory]
    [InlineData("2026-10-25T10:00:00")]
    [InlineData("2026-10-25")]
    public async Task BodyInstant_WithoutOffset_Returns400NamingTheFieldAsync(string at)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.PostAsJsonAsync(EchoBodyUri, new { at }, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
        using var problemDetails = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var error = problemDetails.RootElement.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        error.GetProperty("name").GetString().ShouldBe("at");
        error.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("with a UTC offset or 'Z'");
    }

    [Fact]
    public async Task BodyOptionalInstant_WithoutOffset_Returns400Async()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.PostAsJsonAsync(
            EchoBodyUri, new { at = "2026-10-25T08:00:00Z", until = "2026-10-25T18:30:00" }, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Theory]
    [InlineData("2026-10-25T08:00:00Z")]
    [InlineData("2026-10-25T10:00:00%2B02:00")]
    [InlineData("2026-10-25T03:00:00-05:00")]
    public async Task QueryInstant_WithOffsetOrZ_KeepsTheInstantAsync(string at)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri($"at={at}"), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Instant(body, "at").ShouldBe(EightUtc);
        body.RootElement.GetProperty("until").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("at=2026-10-25T10:00:00")]
    [InlineData("at=2026-10-25T10:00")]
    [InlineData("at=2026-10-25")]
    [InlineData("at=25.10.2026%2010:00%20%2B02:00")]
    [InlineData("at=2026-10-25T08:00:00Z&until=2026-10-25T18:30:00")]
    [InlineData("at=")] // empty required
    public async Task QueryInstant_WithoutOffsetOrNotIso_Returns400Async(string query)
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.AnonymousClient.GetAsync(EchoUri(query), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task QueryOptionalInstant_Empty_BindsTheDefaultInstantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // FastEndpoints' UseDefaultValuesForNullableProps (on by default) binds an empty value of a nullable
        // property to the parser's value without reporting its failure, so an empty DateTimeOffset? is
        // default(DateTimeOffset), never a 400 or a server error.
        using var response = await this.App.AnonymousClient.GetAsync(EchoUri("at=2026-10-25T08:00:00Z&until="), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Instant(body, "until").ShouldBe(default);
    }

    [Fact]
    public async Task HeaderInstants_WithOffset_KeepTheInstantsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = HeaderRequest("2026-10-25T10:00:00+02:00", "2026-10-25T08:00:00Z");
        using var response = await this.App.AnonymousClient.SendAsync(request, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Instant(body, "at").ShouldBe(EightUtc);
        Instant(body, "until").ShouldBe(EightUtc);
    }

    [Theory]
    [InlineData("2026-10-25T10:00:00", null)]
    [InlineData("2026-10-25", null)]
    [InlineData("2026-10-25T08:00:00Z", "2026-10-25T18:30:00")]
    public async Task HeaderInstant_WithoutOffset_Returns400Async(string at, string? until)
    {
        var ct = TestContext.Current.CancellationToken;

        using var request = HeaderRequest(at, until);
        using var response = await this.App.AnonymousClient.SendAsync(request, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task FormInstants_WithOffset_KeepTheInstantsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var form = Form("2026-10-25T03:00:00-05:00", until: null);
        using var response = await this.App.AnonymousClient.PostAsync(EchoFormUri, form, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Instant(body, "at").ShouldBe(EightUtc);
        body.RootElement.GetProperty("until").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("2026-10-25T10:00:00", null)]
    [InlineData("", null)] // empty required
    [InlineData("2026-10-25T08:00:00Z", "2026-10-25")]
    public async Task FormInstant_WithoutOffset_Returns400Async(string at, string? until)
    {
        var ct = TestContext.Current.CancellationToken;

        using var form = Form(at, until);
        using var response = await this.App.AnonymousClient.PostAsync(EchoFormUri, form, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    private static Uri EchoUri(string query) => new($"/api/readings/echo-instant?{query}", UriKind.Relative);

    private static DateTimeOffset Instant(JsonDocument body, string property)
        => DateTimeOffset.Parse(body.RootElement.GetProperty(property).GetString()!, CultureInfo.InvariantCulture);

    private static HttpRequestMessage HeaderRequest(string at, string? until)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/readings/echo-instant-header", UriKind.Relative));
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
