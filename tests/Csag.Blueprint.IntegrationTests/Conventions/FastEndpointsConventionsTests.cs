namespace Csag.Blueprint.IntegrationTests.Conventions;

using System.Net;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Testing.Extensions;

/// <summary>
/// Pins the FastEndpoints routing conventions applied by <c>UseFastEndpointsWithConventions</c>:
/// the global <c>/api</c> route prefix, the <c>[namespace]</c> placeholder resolving to each
/// endpoint's folder name, and kebab-case conversion of multi-word folder names. These conventions
/// are pure configuration — a silent regression would move every route — so each behaviour is
/// asserted through the live routing table.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class FastEndpointsConventionsTests(AppFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public async Task NamespacePlaceholder_ReplacedWithResourceName_ForVehicleEndpoints()
    {
        // ListVehiclesEndpoint lives in Endpoints/Vehicles/List and declares its route as
        // "/[namespace]", which must resolve to /api/vehicles (lowercased folder name).
        var response = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/vehicles", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NamespacePlaceholder_ReplacedWithResourceName_ForMaintenanceRecordEndpoints()
    {
        // ListMaintenanceRecordsEndpoint lives in Endpoints/MaintenanceRecords/List; the
        // multi-word folder name must be converted to a kebab-case route segment.
        var response = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/maintenance-records", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NamespacePlaceholder_ResolvesToTheEndpointFolderNameAsync()
    {
        // "[namespace]" resolves to the endpoint's own folder, so renaming a folder silently
        // rewrites every route beneath it. Pin both halves: the kebab-case form routes, the
        // plain lowercased concatenation does not.
        var current = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/maintenance-records", UriKind.Relative), TestContext.Current.CancellationToken);

        await current.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "ListMaintenanceRecordsEndpoint lives in Endpoints/MaintenanceRecords", TestContext.Current.CancellationToken);

        var concatenated = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/maintenancerecords", UriKind.Relative), TestContext.Current.CancellationToken);

        await concatenated.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, "the non-kebab-case form must not be routable", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GlobalRoutePrefix_AppliedToAllEndpoints()
    {
        // Even anonymous endpoints sit behind the global /api prefix.
        var response = await this.App.AnonymousClient
            .GetAsync(new Uri("/api/localization/greeting", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RoutesWithoutApiPrefix_DoNotExist()
    {
        // The same resources must not be reachable without the /api prefix, proving the global
        // prefix is enforced rather than merely being one of several registered routes.
        var vehicles = await this.App.ViewerAClient
            .GetAsync(new Uri("/vehicles", UriKind.Relative), TestContext.Current.CancellationToken);

        await vehicles.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, cancellationToken: TestContext.Current.CancellationToken);

        var greeting = await this.App.AnonymousClient
            .GetAsync(new Uri("/localization/greeting", UriKind.Relative), TestContext.Current.CancellationToken);

        await greeting.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PostOnlyRoutes_AnswerMethodNotAllowed_ProvingTheRouteResolves()
    {
        // A 405 (MethodNotAllowed) proves the route resolved: the path is registered for POST
        // only, so a GET on it is rejected for its verb rather than falling through to 404.
        var login = await this.App.AnonymousClient
            .GetAsync(new Uri("/api/auth/login", UriKind.Relative), TestContext.Current.CancellationToken);

        await login.ShouldHaveStatusCodeAsync(HttpStatusCode.MethodNotAllowed, cancellationToken: TestContext.Current.CancellationToken);

        var tableViewData = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/vehicles/table-view/data", UriKind.Relative), TestContext.Current.CancellationToken);

        await tableViewData.ShouldHaveStatusCodeAsync(HttpStatusCode.MethodNotAllowed, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownRoute_UnderApiPrefix_Returns404()
    {
        var response = await this.App.AnonymousClient
            .GetAsync(new Uri("/api/no-such-resource", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NestedResourceRoutes_WorkCorrectly()
    {
        // Routes with segments beyond the namespace placeholder ("/[namespace]/{id:guid}") must
        // resolve as well; the seeded City Bike has a fixed id for exactly this kind of assertion.
        var response = await this.App.ViewerAClient
            .GetAsync(new Uri($"/api/vehicles/{SeedData.CityBikeVehicleId}", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }
}
