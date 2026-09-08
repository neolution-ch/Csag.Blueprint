namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Globalization;
using System.Net;

/// <summary>
/// End-to-end tests for correlation-id handling in the request pipeline: the X-Correlation-ID
/// header is accepted (with X-Request-ID as fallback), generated when absent, truncated to the
/// maximum length, and echoed on every response. Headers are set per request instead of on the
/// shared clients so no test can leak a correlation id into another.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class CorrelationIdTests(AppFixture app) : IntegrationTestBase(app)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string RequestIdHeader = "X-Request-ID";

    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task CorrelationIdMiddleware_AcceptsXCorrelationIdHeaderAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedCorrelationId = "test-correlation-123";

        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string> { [CorrelationIdHeader] = expectedCorrelationId },
            ct);

        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue("Response should contain X-Correlation-ID header");
        values!.FirstOrDefault().ShouldBe(expectedCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_AcceptsXRequestIdHeaderAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedCorrelationId = "test-request-456";

        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string> { [RequestIdHeader] = expectedCorrelationId },
            ct);

        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue("Response should contain X-Correlation-ID header");
        values!.FirstOrDefault().ShouldBe(expectedCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_PrefersXCorrelationIdOverXRequestIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedCorrelationId = "correlation-priority";
        const string requestId = "request-fallback";

        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string>
            {
                [CorrelationIdHeader] = expectedCorrelationId,
                [RequestIdHeader] = requestId,
            },
            ct);

        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue();
        values!.FirstOrDefault().ShouldBe(expectedCorrelationId, "Should prefer X-Correlation-ID over X-Request-ID");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesNewIdWhenMissingAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The shared client carries no correlation headers by default.
        using var response = await this.App.ViewerAClient.GetAsync(VehiclesUri, ct);

        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue("Response should contain X-Correlation-ID header");
        var correlationId = values!.FirstOrDefault();
        correlationId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(correlationId, CultureInfo.InvariantCulture, out _).ShouldBeTrue("Generated correlation ID should be a valid GUID");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ReturnsCorrelationIdInResponseHeaderAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedCorrelationId = "response-header-test";

        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string> { [CorrelationIdHeader] = expectedCorrelationId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue();
        values!.FirstOrDefault().ShouldBe(expectedCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_TruncatesLongCorrelationIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The middleware caps correlation ids at 100 characters.
        var longCorrelationId = new string('x', 150);

        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string> { [CorrelationIdHeader] = longCorrelationId },
            ct);

        response.Headers.TryGetValues(CorrelationIdHeader, out var values).ShouldBeTrue();
        var correlationId = values!.FirstOrDefault();
        correlationId.ShouldNotBeNull();
        correlationId.Length.ShouldBe(100, "Correlation ID should be truncated to max length");
        correlationId.ShouldBe(longCorrelationId[..100]);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_AppliedToAllEndpointsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string correlationId = "global-middleware-test";
        var headers = new Dictionary<string, string> { [CorrelationIdHeader] = correlationId };

        // An authorized endpoint, an authenticated endpoint without a policy, and an anonymous
        // endpoint all sit behind the same correlation-id middleware.
        using var vehiclesResponse = await SendGetAsync(this.App.ViewerAClient, VehiclesUri, headers, ct);
        vehiclesResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "Middleware should not break normal endpoint functionality");
        vehiclesResponse.Headers.GetValues(CorrelationIdHeader).FirstOrDefault().ShouldBe(correlationId);

        using var maintenanceResponse = await SendGetAsync(
            this.App.ViewerAClient, new Uri("/api/maintenance-records", UriKind.Relative), headers, ct);
        maintenanceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        maintenanceResponse.Headers.GetValues(CorrelationIdHeader).FirstOrDefault().ShouldBe(correlationId);

        using var greetingResponse = await SendGetAsync(
            this.App.AnonymousClient, new Uri("/api/localization/greeting", UriKind.Relative), headers, ct);
        greetingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        greetingResponse.Headers.GetValues(CorrelationIdHeader).FirstOrDefault().ShouldBe(correlationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_PersistsAcrossRequestLifecycleAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        const string expectedCorrelationId = "lifecycle-test-789";

        // The request flows through the full stack (middleware -> endpoint -> response) and the
        // correlation id must come back unchanged.
        using var response = await SendGetAsync(
            this.App.ViewerAClient,
            VehiclesUri,
            new Dictionary<string, string> { [CorrelationIdHeader] = expectedCorrelationId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdHeader).First().ShouldBe(expectedCorrelationId, "Correlation ID should remain consistent throughout request lifecycle");
    }

    /// <summary>
    /// Sends a GET request with the given headers applied to the single request only, keeping the
    /// shared clients' default headers untouched.
    /// </summary>
    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        Uri uri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return await client.SendAsync(request, ct);
    }
}
