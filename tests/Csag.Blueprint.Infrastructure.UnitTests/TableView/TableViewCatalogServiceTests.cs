namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="TableViewCatalogService"/>. The service treats permissions as opaque
/// strings, so plain literals stand in for an application's permission constants.
/// </summary>
public sealed class TableViewCatalogServiceTests
{
    private const string VehiclesReadPermission = "vehicles:read";
    private const string RentalsReadPermission = "rentals:read";

    [Fact]
    public async Task GetAvailableViewsAsync_WithMatchingPermission_ReturnsView()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        var userPermissions = new[] { VehiclesReadPermission };

        // Act
        var result = await service.GetAvailableViewsAsync(userPermissions, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ViewId.ShouldBe("vehicles");
        result[0].RequiredPermission.ShouldBe(VehiclesReadPermission);
    }

    [Fact]
    public async Task GetAvailableViewsAsync_WithNoMatchingPermissions_ReturnsEmptyList()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        var userPermissions = new[] { RentalsReadPermission };

        // Act
        var result = await service.GetAvailableViewsAsync(userPermissions, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetAvailableViewsAsync_WithMultiplePermissions_ReturnsMatchingViews()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
            new TestTableViewCatalogRegistration("rentals", "Rentals", RentalsReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        var userPermissions = new[] { VehiclesReadPermission, RentalsReadPermission };

        // Act
        var result = await service.GetAvailableViewsAsync(userPermissions, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain(v => v.ViewId == "vehicles");
        result.ShouldContain(v => v.ViewId == "rentals");
    }

    [Fact]
    public async Task GetViewByIdAsync_WithExistingView_ReturnsView()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        // Act
        var result = await service.GetViewByIdAsync("vehicles", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ViewId.ShouldBe("vehicles");
        result.DisplayName.ShouldBe("Vehicles");
        result.EntityType.ShouldBe("TestEntity");
    }

    [Fact]
    public async Task GetViewByIdAsync_WithNonExistingView_ReturnsNull()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        // Act
        var result = await service.GetViewByIdAsync("nonexistent", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetViewByIdAsync_IsCaseInsensitive()
    {
        // Arrange
        var registrations = new List<ITableViewCatalogRegistration>
        {
            new TestTableViewCatalogRegistration("vehicles", "Vehicles", VehiclesReadPermission),
        };

        var service = new TableViewCatalogService(
            registrations,
            new NullLogger<TableViewCatalogService>());

        // Act
        var result = await service.GetViewByIdAsync("VEHICLES", CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ViewId.ShouldBe("vehicles");
    }

    /// <summary>
    /// Test implementation of ITableViewCatalogRegistration for testing purposes.
    /// </summary>
    private sealed class TestTableViewCatalogRegistration : ITableViewCatalogRegistration
    {
        public TestTableViewCatalogRegistration(string viewId, string displayName, string requiredPermission)
        {
            this.ViewId = viewId;
            this.DisplayName = displayName;
            this.RequiredPermission = requiredPermission;
        }

        public string ViewId { get; }

        public string DisplayName { get; }

        public string Description => $"Test view for {this.ViewId}";

        public string RequiredPermission { get; }

        public string EntityType => "TestEntity";
    }
}
