namespace Csag.Blueprint.Testing.Integration;

using FastEndpoints.Testing;

/// <summary>
/// Generic base class for integration tests that automatically resets the database
/// to its post-seeding snapshot state before each test method runs.
/// </summary>
/// <typeparam name="TFixture">
/// The application fixture type. Must implement <see cref="IResettableFixture"/>
/// so the base class can trigger a snapshot restore before each test.
/// </typeparam>
/// <param name="app">The shared application fixture instance injected by xUnit.</param>
public abstract class IntegrationTestBase<TFixture>(TFixture app) : TestBase
    where TFixture : IResettableFixture
{
    /// <summary>
    /// Gets the shared application fixture, providing access to authenticated clients,
    /// services, and other test infrastructure set up during fixture initialization.
    /// </summary>
    protected TFixture App { get; } = app;

    /// <summary>
    /// Runs before each test method to reset the database to its clean snapshot state.
    /// </summary>
    /// <returns>A value task representing the asynchronous setup operation.</returns>
    protected override async ValueTask SetupAsync()
    {
        await this.App.ResetDatabaseAsync();
    }
}
