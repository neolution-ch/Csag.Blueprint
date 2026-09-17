namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unit tests for <see cref="TableViewExecutor"/>.
/// </summary>
public sealed class TableViewExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoFilters_ReturnsAllItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = null,
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        data.Count.ShouldBe(5);
    }

    [Fact]
    public async Task ExecuteAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = null,
            Page = 1,
            PageSize = 2,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithSecondPage_ReturnsCorrectItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = null,
            Page = 2,
            PageSize = 2,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNameFilter_ReturnsMatchingItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "Name", "Cargo" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = null,
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(2);
        data.Count.ShouldBe(2);
        data.All(d => d.Name.Contains("Cargo", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithBooleanFilter_ReturnsMatchingItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "IsActive", "true" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = null,
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(4);
        data.Count.ShouldBe(4);
        data.All(d => d.IsActive).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithEnumFilter_ReturnsMatchingItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "Kind", "Scooter" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = null,
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(2);
        data.Count.ShouldBe(2);
        data.All(d => d.Kind == TestVehicleKind.Scooter).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithRangeFilter_ReturnsMatchingItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "Capacity", "4-6" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = null,
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(3);
        data.Count.ShouldBe(3);
        data.All(d => d.Capacity >= 4 && d.Capacity <= 6).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSortAscending_ReturnsSortedItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        var names = data.Select(d => d.Name).ToList();
        names.ShouldBe(names.OrderBy(n => n).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_WithSortDescending_ReturnsSortedItems()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Desc }],
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        var names = data.Select(d => d.Name).ToList();
        names.ShouldBe(names.OrderByDescending(n => n).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_WithCombinedFilterAndSort_ReturnsCorrectResults()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "IsActive", "true" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = [new SortColumn { ColumnName = "PricePerHour", Direction = SortDirection.Desc }],
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(4);
        data.Count.ShouldBe(4);
        data.All(d => d.IsActive).ShouldBeTrue();

        var rates = data.Select(d => d.PricePerHour).ToList();
        rates.ShouldBe(rates.OrderByDescending(r => r).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_WithPaginationAndFilters_ReturnsCorrectPageAndTotalCount()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();
        var filters = new Dictionary<string, string> { { "IsActive", "true" } };

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = filters,
            SortColumns = null,
            Page = 1,
            PageSize = 2,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(4); // Total matching filter
        data.Count.ShouldBe(2); // Page size
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSortColumns_AppliesPrimaryThenSecondary()
    {
        // Arrange — two Scooter vehicles with the same Kind but different PricePerHour.
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act — sort by Kind asc (groups Scooter rows together), then PricePerHour desc within each group.
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns =
            [
                new SortColumn { ColumnName = "Kind", Direction = SortDirection.Asc },
                new SortColumn { ColumnName = "PricePerHour", Direction = SortDirection.Desc },
            ],
            Page = 1,
            PageSize = 10,
        };
        var (data, _) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert — within each Kind group, rows must be PricePerHour-descending.
        var groups = data.GroupBy(d => d.Kind).ToList();
        groups.ShouldNotBeEmpty();
        foreach (var group in groups)
        {
            var rates = group.Select(d => d.PricePerHour).ToList();
            rates.ShouldBe(rates.OrderByDescending(r => r).ToList());
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSortColumns_MixedDirections_OrdersCorrectly()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns =
            [
                new SortColumn { ColumnName = "Capacity", Direction = SortDirection.Asc },
                new SortColumn { ColumnName = "Name", Direction = SortDirection.Desc },
            ],
            Page = 1,
            PageSize = 10,
        };
        var (data, _) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert — full order matches (Capacity asc, Name desc).
        var expected = data.OrderBy(d => d.Capacity).ThenByDescending(d => d.Name).Select(d => d.Name).ToList();
        var actual = data.Select(d => d.Name).ToList();
        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownSecondSortColumn_AppliesPrimaryAndSkipsUnknown()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act — second column doesn't exist on the definition; should be silently skipped.
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns =
            [
                new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc },
                new SortColumn { ColumnName = "DefinitelyNotARealColumn", Direction = SortDirection.Desc },
            ],
            Page = 1,
            PageSize = 10,
        };
        var (data, _) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert — primary sort still applied.
        var names = data.Select(d => d.Name).ToList();
        names.ShouldBe(names.OrderBy(n => n).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySortColumnsList_BehavesLikeNull()
    {
        // Arrange
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        SeedTestData(scope.Context);
        var executor = new TableViewExecutor();
        var definition = new TestVehicleTableViewDefinition();
        var query = scope.Context.Vehicles.AsNoTracking();

        // Act
        var request = new TableViewQueryRequest<TestVehicle, TestVehicleTableViewDto>
        {
            Query = query,
            Definition = definition,
            Filters = null,
            SortColumns = [],
            Page = 1,
            PageSize = 10,
        };
        var (data, totalCount) = await executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        totalCount.ShouldBe(5);
        data.Count.ShouldBe(5);
    }

    private static void SeedTestData(TestDbContext context)
    {
        var tenantId = TestDbContextFactory.TestTenantId;
        var vehicles = new List<TestVehicle>
        {
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                TenantId = tenantId,
                Name = "Cargo Bike",
                Kind = TestVehicleKind.Bicycle,
                Capacity = 2,
                PricePerHour = 15.00m,
                IsActive = true,
                AcquiredAt = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                TenantId = tenantId,
                Name = "City Scooter",
                Kind = TestVehicleKind.Scooter,
                Capacity = 4,
                PricePerHour = 25.00m,
                IsActive = true,
                AcquiredAt = new DateTime(2023, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                TenantId = tenantId,
                Name = "Sea Kayak",
                Kind = TestVehicleKind.Kayak,
                Capacity = 6,
                PricePerHour = 35.00m,
                IsActive = false,
                AcquiredAt = new DateTime(2023, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                TenantId = tenantId,
                Name = "Touring Scooter",
                Kind = TestVehicleKind.Scooter,
                Capacity = 4,
                PricePerHour = 20.00m,
                IsActive = true,
                AcquiredAt = new DateTime(2023, 8, 5, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                TenantId = tenantId,
                Name = "Cargo Trike",
                Kind = TestVehicleKind.Bicycle,
                Capacity = 2,
                PricePerHour = 12.00m,
                IsActive = true,
                AcquiredAt = new DateTime(2023, 10, 12, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        context.Vehicles.AddRange(vehicles);
        context.SaveChanges();
    }
}
