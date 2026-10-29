namespace Csag.Blueprint.IntegrationTests.Conventions;

using System.Net;
using Csag.Blueprint.TestHost.Plugin;
using Csag.Blueprint.Testing.Extensions;
using FastEndpoints;

/// <summary>
/// Pins the additive-discovery contract of <c>BlueprintServicesOptions.FastEndpointsAdditionalAssemblies</c>.
/// <c>ExternalMarkerEndpoint</c> lives in the separate <c>Csag.Blueprint.TestHost.Plugin</c> assembly.
/// <c>Program.cs</c> lists that assembly there. A passing route proves it is discovered. The host's
/// own endpoint proves the entry assembly is still scanned too.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ExternalAssemblyDiscoveryTests(AppFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public async Task ExternalAssemblyEndpoint_IsDiscoveredAndReachableAsync()
    {
        var (rsp, res) = await this.App.AnonymousClient.GETAsync<ExternalMarkerEndpoint, ExternalMarkerResponse>();

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
        res.Marker.ShouldBe("external-marker");
    }

    [Fact]
    public async Task HostOwnAssemblyEndpoint_RemainsDiscoveredAlongsideTheExternalOneAsync()
    {
        // ListVehiclesEndpoint lives in the TestHost entry assembly. Discovering the external
        // assembly above must not come at the entry assembly's expense.
        var response = await this.App.ViewerAClient
            .GetAsync(new Uri("/api/vehicles", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }
}
