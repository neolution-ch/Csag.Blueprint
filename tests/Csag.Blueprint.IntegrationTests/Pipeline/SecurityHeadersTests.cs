namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;

/// <summary>
/// End-to-end tests for the security-header middleware: the hardening headers are present on real
/// responses and the server-identity headers are stripped. The TestHost enables both behaviors via
/// Blueprint:Security:SecurityHeaders, so these tests exercise the actual middleware rather than
/// configuration binding.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SecurityHeadersTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task SecurityHeaders_AppliedToAllResponsesAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.ViewerAClient.GetAsync(VehiclesUri, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions).ShouldBeTrue("X-Content-Type-Options header should be present");
        contentTypeOptions!.FirstOrDefault().ShouldBe("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var frameOptions).ShouldBeTrue("X-Frame-Options header should be present");
        frameOptions!.FirstOrDefault().ShouldBe("DENY");

        response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy).ShouldBeTrue("Referrer-Policy header should be present");
        referrerPolicy!.FirstOrDefault().ShouldBe("strict-origin-when-cross-origin");

        response.Headers.TryGetValues("Permissions-Policy", out var permissionsPolicy).ShouldBeTrue("Permissions-Policy header should be present");
        permissionsPolicy!.FirstOrDefault().ShouldBe("geolocation=(), microphone=(), camera=()");
    }

    [Fact]
    public async Task ServerIdentityHeaders_RemovedFromResponsesAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.ViewerAClient.GetAsync(VehiclesUri, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Server").ShouldBeFalse("Server header should be removed");
        response.Headers.Contains("X-Powered-By").ShouldBeFalse("X-Powered-By header should be removed");
    }
}
