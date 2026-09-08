namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="BlueprintTableViewPreferencesService{TContext, TUser}"/>.
/// </summary>
public sealed class TableViewPreferencesServiceTests : IDisposable
{
    private readonly TestDbContextScope<TestDbContext> scope;
    private readonly BlueprintTableViewPreferencesService<TestDbContext, TestUser> service;

    public TableViewPreferencesServiceTests()
    {
        this.scope = TestDbContextFactory.CreateInMemoryDbContext();
        this.service = new BlueprintTableViewPreferencesService<TestDbContext, TestUser>(
            this.scope.Context,
            new NullLogger<BlueprintTableViewPreferencesService<TestDbContext, TestUser>>());
    }

    public void Dispose()
    {
        this.scope.Dispose();
    }

    [Fact]
    public async Task GetPreferenceByIdAsync_WithExistingPreference_ReturnsPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            IsDefault = true,
            Columns =
            [
                new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 },
                new ColumnPreference { Name = "Kind", IsVisible = false, Order = 1 },
            ],
            Filters = new Dictionary<string, string> { { "Name", "test" } },
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
            PageSize = 25,
            Version = "1.0",
        };

        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Act
        var result = await this.service.GetPreferenceByIdAsync(userId, preferenceId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.TableViewId.ShouldBe(tableViewId);
        result.Columns.Count.ShouldBe(2);
        result.Filters.ShouldContainKey("Name");
        result.SortColumns.Count.ShouldBe(1);
        result.SortColumns[0].ColumnName.ShouldBe("Name");
        result.SortColumns[0].Direction.ShouldBe(SortDirection.Asc);
        result.PageSize.ShouldBe(25);
    }

    [Fact]
    public async Task GetPreferenceByIdAsync_WithMultipleSortColumns_RoundTrips()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "Multi-sort view",
            Columns = [new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 }],
            SortColumns =
            [
                new SortColumn { ColumnName = "Kind", Direction = SortDirection.Asc },
                new SortColumn { ColumnName = "PricePerHour", Direction = SortDirection.Desc },
            ],
            Version = "1.0",
        };

        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Act
        var result = await this.service.GetPreferenceByIdAsync(userId, preferenceId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SortColumns.Count.ShouldBe(2);
        result.SortColumns[0].ColumnName.ShouldBe("Kind");
        result.SortColumns[0].Direction.ShouldBe(SortDirection.Asc);
        result.SortColumns[1].ColumnName.ShouldBe("PricePerHour");
        result.SortColumns[1].Direction.ShouldBe(SortDirection.Desc);
    }

    [Fact]
    public async Task GetPreferenceByIdAsync_WithNoPreference_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await this.service.GetPreferenceByIdAsync(userId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreatePreferenceAsync_CreatesNewPreference()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            Columns = [new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 }],
            Version = "1.0",
        };

        // Act
        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Assert
        preferenceId.ShouldNotBe(Guid.Empty);
        var saved = await this.scope.Context.TableViewPreferences
            .FirstOrDefaultAsync(p => p.Id == preferenceId, TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.UserId.ShouldBe(userId);
        saved.TableViewId.ShouldBe(tableViewId);
        saved.PreferencesJson.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdatePreferenceAsync_UpdatesExistingPreference()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var initialPreferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            Columns = [new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 }],
            PageSize = 10,
            Version = "1.0",
        };

        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, initialPreferences, TestContext.Current.CancellationToken);

        var updatedPreferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View Updated",
            Columns =
            [
                new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 },
                new ColumnPreference { Name = "Kind", IsVisible = true, Order = 1 },
            ],
            PageSize = 50,
            Version = "1.0",
        };

        // Act
        var result = await this.service.UpdatePreferenceAsync(userId, preferenceId, updatedPreferences, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        var saved = await this.service.GetPreferenceByIdAsync(userId, preferenceId, TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.Columns.Count.ShouldBe(2);
        saved.PageSize.ShouldBe(50);
    }

    [Fact]
    public async Task DeletePreferenceAsync_WithExistingPreference_DeletesAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            Columns = [new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 }],
            Version = "1.0",
        };

        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Act
        var result = await this.service.DeletePreferenceAsync(userId, preferenceId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
        var existing = await this.service.GetPreferenceByIdAsync(userId, preferenceId, TestContext.Current.CancellationToken);
        existing.ShouldBeNull();
    }

    [Fact]
    public async Task DeletePreferenceAsync_WithNoPreference_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await this.service.DeletePreferenceAsync(userId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllPreferencesAsync_WithExistingPreferences_ReturnsList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            Columns = [new ColumnPreference { Name = "Name", IsVisible = true, Order = 0 }],
            Version = "1.0",
        };

        await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Act
        var result = await this.service.GetAllPreferencesAsync(userId, tableViewId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAllPreferencesAsync_WithNoPreferences_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";

        // Act
        var result = await this.service.GetAllPreferencesAsync(userId, tableViewId, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreatePreferenceAsync_SetsCorrectTimestamps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tableViewId = "vehicles";
        var preferences = new TableViewPreferencesModel
        {
            TableViewId = tableViewId,
            Name = "My View",
            Columns = [],
            Version = "1.0",
        };

        var beforeSave = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        var preferenceId = await this.service.CreatePreferenceAsync(userId, tableViewId, preferences, TestContext.Current.CancellationToken);

        // Assert
        var saved = await this.scope.Context.TableViewPreferences
            .FirstOrDefaultAsync(p => p.Id == preferenceId, TestContext.Current.CancellationToken);
        saved.ShouldNotBeNull();
        saved.CreatedAt.ShouldBeGreaterThan(beforeSave);
    }
}
