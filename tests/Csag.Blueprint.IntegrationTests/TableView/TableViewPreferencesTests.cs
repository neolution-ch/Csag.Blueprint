namespace Csag.Blueprint.IntegrationTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service-level tests for the table view preferences store
/// (<c>BlueprintTableViewPreferencesService</c>) against the container database. The service is
/// resolved through the host's DI container, so rows are written through the host's own context
/// and interceptors; the snapshot restore before each test guarantees a clean preferences table.
/// Preferences are keyed by user id, not tenant, so the seeded user ids from
/// <see cref="SeedData"/> drive the ownership assertions.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TableViewPreferencesTests(AppFixture app) : IntegrationTestBase(app)
{
    private const string VehiclesViewId = "vehicles";

    [Fact]
    public async Task Preferences_SaveAndReadBack_RoundTripsAllFieldsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITableViewPreferencesService>();

        var preferenceId = await service.CreatePreferenceAsync(
            SeedData.ManagerAUserId,
            VehiclesViewId,
            CreateModel("My Kayak View", isDefault: true),
            ct);

        preferenceId.ShouldNotBe(Guid.Empty);

        // Read back by id — every persisted field must survive the JSON round trip.
        var byId = (await service.GetPreferenceByIdAsync(SeedData.ManagerAUserId, preferenceId, ct)).ShouldNotBeNull();
        byId.TableViewId.ShouldBe(VehiclesViewId);
        byId.Name.ShouldBe("My Kayak View");
        byId.IsDefault.ShouldBeTrue();
        byId.Columns.Count.ShouldBe(3);
        byId.Columns.First(c => c.Name == "Name").Width.ShouldBe(200);
        byId.Columns.First(c => c.Name == "Kind").IsPinned.ShouldBeTrue();
        byId.Columns.First(c => c.Name == "Capacity").IsVisible.ShouldBeFalse();
        byId.Filters.ShouldContainKeyAndValue("Kind", "Kayak");
        byId.SortColumns.Count.ShouldBe(1);
        byId.SortColumns[0].ColumnName.ShouldBe("Name");
        byId.SortColumns[0].Direction.ShouldBe(SortDirection.Asc);
        byId.PageSize.ShouldBe(25);

        // The default lookup and the summary listing must surface the same saved view.
        var asDefault = (await service.GetDefaultPreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, ct)).ShouldNotBeNull();
        asDefault.Name.ShouldBe("My Kayak View");

        var summaries = await service.GetAllPreferencesAsync(SeedData.ManagerAUserId, VehiclesViewId, ct);
        summaries.Count.ShouldBe(1);
        summaries[0].Id.ShouldBe(preferenceId);
        summaries[0].Name.ShouldBe("My Kayak View");
        summaries[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Preferences_UpdateAndDelete_FullLifecycleAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITableViewPreferencesService>();

        var keepId = await service.CreatePreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, CreateModel("Keep Me"), ct);
        var deleteId = await service.CreatePreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, CreateModel("Delete Me"), ct);

        // Update the first view's name and page size.
        var updatedModel = CreateModel("Keep Me (renamed)");
        updatedModel.PageSize = 50;
        (await service.UpdatePreferenceAsync(SeedData.ManagerAUserId, keepId, updatedModel, ct)).ShouldBeTrue();

        var updated = (await service.GetPreferenceByIdAsync(SeedData.ManagerAUserId, keepId, ct)).ShouldNotBeNull();
        updated.Name.ShouldBe("Keep Me (renamed)");
        updated.PageSize.ShouldBe(50);

        // Delete the second view; the first must be unaffected.
        (await service.DeletePreferenceAsync(SeedData.ManagerAUserId, deleteId, ct)).ShouldBeTrue();

        var summaries = await service.GetAllPreferencesAsync(SeedData.ManagerAUserId, VehiclesViewId, ct);
        summaries.Count.ShouldBe(1);
        summaries[0].Name.ShouldBe("Keep Me (renamed)");

        // Operations on rows that no longer exist report failure instead of throwing.
        (await service.DeletePreferenceAsync(SeedData.ManagerAUserId, deleteId, ct)).ShouldBeFalse();
        (await service.UpdatePreferenceAsync(SeedData.ManagerAUserId, deleteId, updatedModel, ct)).ShouldBeFalse();
        (await service.GetPreferenceByIdAsync(SeedData.ManagerAUserId, deleteId, ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Preferences_SetDefault_OnlyOneDefaultPerViewAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITableViewPreferencesService>();

        var firstId = await service.CreatePreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, CreateModel("First", isDefault: true), ct);
        var secondId = await service.CreatePreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, CreateModel("Second"), ct);

        (await service.SetDefaultAsync(SeedData.ManagerAUserId, VehiclesViewId, secondId, ct)).ShouldBeTrue();

        // The previous default must have been unset — exactly one default remains.
        var first = (await service.GetPreferenceByIdAsync(SeedData.ManagerAUserId, firstId, ct)).ShouldNotBeNull();
        first.IsDefault.ShouldBeFalse();

        var summaries = await service.GetAllPreferencesAsync(SeedData.ManagerAUserId, VehiclesViewId, ct);
        summaries.Count(s => s.IsDefault).ShouldBe(1);
        summaries.First(s => s.IsDefault).Name.ShouldBe("Second");

        var asDefault = (await service.GetDefaultPreferenceAsync(SeedData.ManagerAUserId, VehiclesViewId, ct)).ShouldNotBeNull();
        asDefault.Name.ShouldBe("Second");

        // Setting a nonexistent preference as default reports failure and changes nothing.
        (await service.SetDefaultAsync(SeedData.ManagerAUserId, VehiclesViewId, Guid.NewGuid(), ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Preferences_PerUserSeparation_UsersOnlySeeTheirOwnViewsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITableViewPreferencesService>();

        var managerPreferenceId = await service.CreatePreferenceAsync(
            SeedData.ManagerAUserId, VehiclesViewId, CreateModel("Manager View", isDefault: true), ct);
        await service.CreatePreferenceAsync(
            SeedData.ViewerAUserId, VehiclesViewId, CreateModel("Viewer View"), ct);

        // Each user's listing contains only their own rows.
        var managerSummaries = await service.GetAllPreferencesAsync(SeedData.ManagerAUserId, VehiclesViewId, ct);
        managerSummaries.Count.ShouldBe(1);
        managerSummaries[0].Name.ShouldBe("Manager View");

        var viewerSummaries = await service.GetAllPreferencesAsync(SeedData.ViewerAUserId, VehiclesViewId, ct);
        viewerSummaries.Count.ShouldBe(1);
        viewerSummaries[0].Name.ShouldBe("Viewer View");

        // The user id acts as an ownership check: another user's preference is unreachable
        // for reading, deleting, and default resolution.
        (await service.GetPreferenceByIdAsync(SeedData.ViewerAUserId, managerPreferenceId, ct)).ShouldBeNull();
        (await service.DeletePreferenceAsync(SeedData.ViewerAUserId, managerPreferenceId, ct)).ShouldBeFalse();
        (await service.GetDefaultPreferenceAsync(SeedData.ViewerAUserId, VehiclesViewId, ct)).ShouldBeNull();

        // The failed cross-user delete must not have removed the manager's row.
        (await service.GetPreferenceByIdAsync(SeedData.ManagerAUserId, managerPreferenceId, ct)).ShouldNotBeNull();
    }

    /// <summary>
    /// Builds a fully populated preferences model so round-trip assertions cover columns
    /// (visibility, order, width, pinning), filters, sorting, and page size.
    /// </summary>
    /// <param name="name">The user-given name of the saved view.</param>
    /// <param name="isDefault">Whether the view is the user's default for the table.</param>
    /// <returns>The preferences model.</returns>
    private static TableViewPreferencesModel CreateModel(string name, bool isDefault = false)
    {
        return new TableViewPreferencesModel
        {
            Name = name,
            IsDefault = isDefault,
            Columns =
            [
                new ColumnPreference { Name = "Name", IsVisible = true, Order = 0, Width = 200 },
                new ColumnPreference { Name = "Kind", IsVisible = true, Order = 1, IsPinned = true },
                new ColumnPreference { Name = "Capacity", IsVisible = false, Order = 2 },
            ],
            Filters = new Dictionary<string, string> { ["Kind"] = "Kayak" },
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
            PageSize = 25,
        };
    }
}
