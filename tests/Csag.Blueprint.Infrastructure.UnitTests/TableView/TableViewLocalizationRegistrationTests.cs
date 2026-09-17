namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.Localization;
using Csag.Blueprint.Infrastructure.TableView;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for the interplay between the table view core registration and the database-backed
/// localization registration of <see cref="ITableViewMetadataLocalizer"/>.
/// </summary>
public sealed class TableViewLocalizationRegistrationTests
{
    [Fact]
    public void MetadataLocalizer_UpgradesToStringLocalizerImplementation_RegardlessOfRegistrationOrder()
    {
        // AddBlueprintTableViewCore uses TryAdd (no-op default) and AddBlueprintDbLocalization uses
        // Replace, so the localizer-backed implementation must win in either registration order.
        foreach (var tableViewFirst in new[] { true, false })
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            if (tableViewFirst)
            {
                services.AddBlueprintTableViewCore();
                services.AddBlueprintDbLocalization<TestDbContext>("en-GB", new Dictionary<string, string>(), 2);
            }
            else
            {
                services.AddBlueprintDbLocalization<TestDbContext>("en-GB", new Dictionary<string, string>(), 2);
                services.AddBlueprintTableViewCore();
            }

            // Assert — exactly one registration remains and it is the factory-based (localizer-backed)
            // one, not the no-op implementation type.
            var descriptors = services.Where(d => d.ServiceType == typeof(ITableViewMetadataLocalizer)).ToList();
            descriptors.Count.ShouldBe(1, $"tableViewFirst={tableViewFirst}");
            descriptors[0].ImplementationType.ShouldNotBe(typeof(NoOpTableViewMetadataLocalizer), $"tableViewFirst={tableViewFirst}");
            descriptors[0].ImplementationFactory.ShouldNotBeNull($"tableViewFirst={tableViewFirst}");
        }
    }
}
