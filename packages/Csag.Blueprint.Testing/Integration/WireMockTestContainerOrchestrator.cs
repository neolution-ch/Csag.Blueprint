namespace Csag.Blueprint.Testing.Integration;

using WireMock.Client;
using WireMock.Net.Testcontainers;

/// <summary>
/// Manages the lifecycle of a WireMock Testcontainer for integration tests,
/// including container start and per-test state reset.
/// </summary>
/// <remarks>
/// Intended usage pattern:
/// <list type="number">
///   <item><description>Call <see cref="StartAsync"/> in <c>PreSetupAsync</c>.</description></item>
///   <item><description>Use <see cref="GetPublicUri"/> in <c>ConfigureApp</c> to point the SUT at WireMock.</description></item>
///   <item><description>Call <see cref="CreateFreshAdminClientAsync"/> at the start of each test to get a clean stub environment.</description></item>
///   <item><description>Call <see cref="DisposeAsync"/> in <c>TearDownAsync</c>.</description></item>
/// </list>
/// </remarks>
public sealed class WireMockTestContainerOrchestrator : IAsyncDisposable
{
    private readonly WireMockContainer container;

    /// <summary>
    /// Initializes a new instance of the <see cref="WireMockTestContainerOrchestrator"/> class
    /// with auto-remove and cleanup enabled.
    /// </summary>
    public WireMockTestContainerOrchestrator()
    {
        this.container = new WireMockContainerBuilder()
            .WithImage("sheyenrath/wiremock.net-alpine:2.2.0")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the public base URI of the running WireMock container.
    /// Use this to override external service base URIs in the application configuration during tests.
    /// </summary>
    /// <returns>The public URI of the WireMock server.</returns>
    public Uri GetPublicUri() => new Uri(this.container.GetPublicUrl());

    /// <summary>
    /// Starts the WireMock Testcontainer.
    /// </summary>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync() => this.container.StartAsync();

    /// <summary>
    /// Creates a strongly-typed WireMock admin client and resets all mappings, scenarios, and recorded requests.
    /// Call this at the start of each test that configures WireMock stubs to ensure test isolation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the reset operations.</param>
    /// <returns>A fresh <see cref="IWireMockAdminApi"/> ready for stub configuration.</returns>
    public async Task<IWireMockAdminApi> CreateFreshAdminClientAsync(CancellationToken cancellationToken = default)
    {
        var adminClient = this.container.CreateWireMockAdminClient();
        await adminClient.DeleteMappingsAsync(cancellationToken);
        await adminClient.ResetScenariosAsync(cancellationToken);
        await adminClient.ResetRequestsAsync(cancellationToken);
        return adminClient;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => this.container.DisposeAsync();
}
