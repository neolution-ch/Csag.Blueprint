namespace Csag.Blueprint.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.TestHost.Endpoints.Localization.Greeting;
using Csag.Blueprint.TestHost.Endpoints.Vehicles;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// End-to-end smoke tests proving the TestHost boots against the SQL container and the full
/// cookie-session pipeline works: readiness, login with auth and CSRF cookies, permission-based
/// authorization, and a real write/read round trip through the tenant-scoped vehicle endpoints.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SmokeTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task HealthEndpoints_ReadyzAndLivez_ReturnOk()
    {
        var ct = TestContext.Current.CancellationToken;

        // The fixture already waited for the startup gate, so readiness converges quickly; the
        // small retry loop keeps the assertion robust on slow runners.
        var readyz = await GetUntilOkAsync(this.App.AnonymousClient, new Uri("/health/readyz", UriKind.Relative), ct);
        await readyz.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var livez = await this.App.AnonymousClient.GetAsync(new Uri("/health/livez", UriKind.Relative), ct);
        await livez.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
    }

    [Fact]
    public async Task Login_WithSeededCredentials_SetsSessionAndCsrfCookies()
    {
        using var client = this.App.CreateClient();

        var (rsp, body) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = SeedData.ManagerAEmail,
                Password = SeedData.DefaultPassword,
            });

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);

        rsp.Headers.TryGetValues("Set-Cookie", out var setCookies).ShouldBeTrue();
        var cookies = setCookies!.ToList();
        cookies.ShouldContain(c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        cookies.ShouldContain(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        body.IsAuthenticated.ShouldBeTrue();
        body.Email.ShouldBe(SeedData.ManagerAEmail);
        body.CurrentTenantId.ShouldBe(SeedData.TenantAId);
        body.Roles.ShouldContain("TenantManager");
        body.Permissions.ShouldContain("vehicles:manage");
    }

    [Fact]
    public async Task CreateVehicle_AsManagerA_PersistsAndReadsBack()
    {
        var ct = TestContext.Current.CancellationToken;

        var request = new CreateVehicleRequest
        {
            Name = "Smoke Test Canoe",
            Kind = TestVehicleKind.Kayak,
            Capacity = 3,
            PricePerHour = 21.50m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
        };

        var createResponse = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, request, BlueprintJsonOptions.Default, ct);
        await createResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, cancellationToken: ct);

        var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>(BlueprintJsonOptions.Default, ct);
        created.ShouldNotBeNull();
        created.Name.ShouldBe(request.Name);
        created.Kind.ShouldBe(TestVehicleKind.Kayak);

        var readResponse = await this.App.ManagerAClient.GetAsync(new Uri($"/api/vehicles/{created.Id}", UriKind.Relative), ct);
        await readResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var read = await readResponse.Content.ReadFromJsonAsync<VehicleResponse>(BlueprintJsonOptions.Default, ct);
        read.ShouldNotBeNull();
        read.Id.ShouldBe(created.Id);
        read.Name.ShouldBe(request.Name);
        read.Capacity.ShouldBe(3);
        read.PricePerHour.ShouldBe(21.50m);

        // The fixture's database scope sees the same tenant-scoped world the endpoint wrote to.
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        (await scope.Context.Vehicles.CountAsync(ct)).ShouldBe(SeedData.TenantAVehicleCount + 1);
        (await scope.Context.Vehicles.SingleAsync(v => v.Id == created.Id, ct)).TenantId.ShouldBe(SeedData.TenantAId);
    }

    [Fact]
    public async Task Greeting_WithGermanAcceptLanguage_ResolvesEveryFallbackTier()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = this.CreateClientForCulture("de");
        var response = await client.GetAsync(new Uri("/api/localization/greeting", UriKind.Relative), ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var greeting = await response.Content.ReadFromJsonAsync<GreetingResponse>(BlueprintJsonOptions.Default, ct);
        greeting.ShouldNotBeNull();
        greeting.Culture.ShouldBe("de");

        // Requested-language row wins; a key without a German row falls back to the
        // default-language row; a key with no rows at all falls back to the code-defined default.
        greeting.Hello.ShouldBe("Hallo aus der Datenbank");
        greeting.EnglishOnly.ShouldBe("This value exists only in English");
        greeting.CodeOnly.ShouldBe("Code-only greeting");
    }

    [Fact]
    public async Task VehicleTableView_AsViewerA_ReturnsSeededRows()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await this.App.ViewerAClient.PostAsJsonAsync(
            new Uri("/api/vehicles/table-view/data", UriKind.Relative),
            new TableViewDataRequest { Page = 1, PageSize = 10 },
            BlueprintJsonOptions.Default,
            ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        var result = await response.Content.ReadFromJsonAsync<TableViewDataResponse<VehicleTableViewDto>>(BlueprintJsonOptions.Default, ct);
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(SeedData.TenantAVehicleCount);
        result.Data.Count.ShouldBe(SeedData.TenantAVehicleCount);
        result.Metadata.ShouldContain(c => c.Name == "Name");
    }

    [Fact]
    public async Task CreateVehicle_AsViewerA_IsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;

        var request = new CreateVehicleRequest
        {
            Name = "Forbidden Vehicle",
            Kind = TestVehicleKind.Bicycle,
            Capacity = 1,
            PricePerHour = 5m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
        };

        var response = await this.App.ViewerAClient.PostAsJsonAsync(VehiclesUri, request, BlueprintJsonOptions.Default, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: ct);
    }

    [Fact]
    public async Task ListVehicles_AsAnonymous_IsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await this.App.AnonymousClient.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);
    }

    /// <summary>
    /// Polls the given URL until it returns 200 OK or the retry budget is exhausted, returning the
    /// last response either way.
    /// </summary>
    private static async Task<HttpResponseMessage> GetUntilOkAsync(HttpClient client, Uri url, CancellationToken ct)
    {
        const int maxAttempts = 30;

        HttpResponseMessage response = await client.GetAsync(url, ct);
        for (var attempt = 1; attempt < maxAttempts && response.StatusCode != HttpStatusCode.OK; attempt++)
        {
            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            response = await client.GetAsync(url, ct);
        }

        return response;
    }
}
