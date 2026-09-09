namespace Csag.Blueprint.IntegrationTests;

/// <summary>
/// Defines the test collection sharing a single <see cref="AppFixture"/> instance across all test
/// classes, so one SQL Server container serves the entire run instead of one per test class.
/// Every integration test class must carry <c>[Collection(nameof(AppFixtureCollection))]</c>.
/// </summary>
[CollectionDefinition(nameof(AppFixtureCollection))]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — xUnit convention names collection definitions with the 'Collection' suffix.
public class AppFixtureCollection : ICollectionFixture<AppFixture>
#pragma warning restore CA1711
{
    // Never instantiated; exists purely to define the collection for xUnit.
}
