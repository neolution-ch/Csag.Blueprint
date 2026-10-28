namespace Csag.Blueprint.IntegrationTests.TableView;

using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.TableView;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;
using FastEndpoints;

/// <summary>
/// Cross-cutting integration tests for the table view infrastructure — generic base endpoints,
/// executor, metadata, and Excel export — exercised once against the seeded vehicles. Any entity
/// built on the same base classes inherits the same behaviour by construction. The vehicles
/// definition covers every filter operator (equals, contains, enum, range, boolean, date range),
/// so the filter scenarios here span the executor's whole surface.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TableViewTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri ExportUri = new("/api/vehicles/table-view/export", UriKind.Relative);

    [Fact]
    public async Task DataEndpoint_BasicQuery_ReturnsAllSeededVehiclesAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 50 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.TotalCount.ShouldBe(SeedData.TenantAVehicleCount);
        res.Data.Count.ShouldBe(SeedData.TenantAVehicleCount);
        res.Page.ShouldBe(1);
        res.PageSize.ShouldBe(50);
        res.TotalPages.ShouldBe(1);
        res.Metadata.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DataEndpoint_DefaultPaging_UsesFirstPageOfTenAsync()
    {
        // An empty request falls back to the request defaults: page 1, page size 10.
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest());

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Page.ShouldBe(1);
        res.PageSize.ShouldBe(10);
        res.TotalCount.ShouldBe(SeedData.TenantAVehicleCount);
        res.Data.Count.ShouldBe(SeedData.TenantAVehicleCount);
    }

    [Fact]
    public async Task DataEndpoint_Pagination_FirstPage_ReturnsCorrectSliceAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 2 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Data.Count.ShouldBe(2);
        res.TotalCount.ShouldBe(SeedData.TenantAVehicleCount);
        res.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task DataEndpoint_Pagination_LastPage_ReturnsRemainingItemsAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 3, PageSize = 2 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Data.Count.ShouldBe(2);
        res.Page.ShouldBe(3);
        res.TotalCount.ShouldBe(SeedData.TenantAVehicleCount);
    }

    [Fact]
    public async Task DataEndpoint_SortByNameAsc_ReturnsAlphabeticalOrderAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Data.First().Name.ShouldBe("Cargo Scooter");
        res.Data.Last().Name.ShouldBe("Tandem Bike");
    }

    [Fact]
    public async Task DataEndpoint_SortByNameDesc_ReturnsReverseOrderAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Desc }],
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Data.First().Name.ShouldBe("Tandem Bike");
        res.Data.Last().Name.ShouldBe("Cargo Scooter");
    }

    [Fact]
    public async Task DataEndpoint_SortByMultipleColumns_AppliesPrimaryThenSecondaryAsync()
    {
        // Sort by Kind asc (numeric enum order), then Name desc within each kind group.
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    SortColumns =
                    [
                        new SortColumn { ColumnName = "Kind", Direction = SortDirection.Asc },
                        new SortColumn { ColumnName = "Name", Direction = SortDirection.Desc },
                    ],
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Primary sort: the enum values must be non-decreasing over the whole page.
        var kinds = res.Data.Select(d => (int)d.Kind).ToList();
        kinds.ShouldBe(kinds.OrderBy(k => k).ToList());

        // Secondary sort: within each kind group, names must be in descending order.
        foreach (var group in res.Data.GroupBy(d => d.Kind))
        {
            var names = group.Select(d => d.Name).ToList();
            names.ShouldBe(names.OrderByDescending(n => n, StringComparer.Ordinal).ToList());
        }
    }

    [Fact]
    public async Task DataEndpoint_FilterByNameContains_ReturnsMatchingVehiclesAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    Filters = new Dictionary<string, string> { ["Name"] = "Bike" },
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.TotalCount.ShouldBe(2);
        res.Data.ShouldAllBe(d => d.Name.Contains("Bike", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DataEndpoint_FilterByKindEnum_ReturnsOnlyThatKindAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    Filters = new Dictionary<string, string> { ["Kind"] = nameof(TestVehicleKind.Kayak) },
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.TotalCount.ShouldBe(2);
        res.Data.ShouldAllBe(d => d.Kind == TestVehicleKind.Kayak);
    }

    [Fact]
    public async Task DataEndpoint_FilterByIsActive_ReturnsOnlyActiveVehiclesAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    Filters = new Dictionary<string, string> { ["IsActive"] = "true" },
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Tenant A seeds six vehicles of which two are inactive (Cargo Scooter, Family Kayak).
        res.TotalCount.ShouldBe(4);
        res.Data.ShouldAllBe(d => d.IsActive);
    }

    [Fact]
    public async Task DataEndpoint_FilterByPriceRange_ReturnsVehiclesWithinRangeAsync()
    {
        // Range filter values use the "min-max" wire format.
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Page = 1,
                    PageSize = 50,
                    Filters = new Dictionary<string, string> { ["PricePerHour"] = "10-20" },
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Seeded prices in tenant A: 6.50, 9.00, 12.00, 14.50, 18.00, 25.00 — three fall in range.
        res.TotalCount.ShouldBe(3);
        res.Data.ShouldAllBe(d => d.PricePerHour >= 10m && d.PricePerHour <= 20m);
    }

    [Fact]
    public async Task DataEndpoint_Projection_AllDtoFieldsPopulatedAsync()
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 50 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // City Bike is the one seeded vehicle with a fixed id, so every projected field can be
        // asserted against known values.
        var cityBike = res.Data.First(d => d.Name == "City Bike");
        cityBike.Id.ShouldBe(SeedData.CityBikeVehicleId);
        cityBike.Kind.ShouldBe(TestVehicleKind.Bicycle);
        cityBike.Capacity.ShouldBe(1);
        cityBike.PricePerHour.ShouldBe(6.50m);
        cityBike.IsActive.ShouldBeTrue();
        cityBike.AcquiredAt.ShouldBe(new DateTime(2023, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        cityBike.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task DataEndpoint_TenantIsolation_ManagerBSeesOnlyTenantBVehiclesAsync()
    {
        var (rsp, res) = await this.App.ManagerBClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 50 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.TotalCount.ShouldBe(SeedData.TenantBVehicleCount);
        res.Data.Count.ShouldBe(SeedData.TenantBVehicleCount);
        res.Data.ShouldContain(d => d.Name == "Harbor Kayak");
        res.Data.ShouldContain(d => d.Name == "Downtown Scooter");
    }

    [Fact]
    public async Task DataEndpoint_UnauthenticatedUser_Returns401Async()
    {
        var (rsp, _) = await this.App.AnonymousClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 10 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DataEndpoint_WithoutVehiclesReadPermission_Returns403Async()
    {
        // The platform admin is authenticated and a member of tenant A, but holds no tenant role
        // there and therefore lacks the vehicles:read permission the endpoint's policy requires.
        var (rsp, _) = await this.App.PlatformClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 10 });

        rsp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MetadataEndpoint_ReturnsAllColumnDefinitionsAsync()
    {
        var (rsp, res) = await this.App.ViewerAClient
            .GETAsync<VehicleTableViewMetadataEndpoint, TableViewMetadataResponse>();

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ViewId.ShouldBe("vehicles");
        res.DisplayName.ShouldBe("Vehicles");
        res.Description.ShouldNotBeNullOrWhiteSpace();
        res.Columns.Count.ShouldBe(8);

        // String column: contains filter with a plain text input.
        var nameColumn = res.Columns.First(c => c.Name == "Name");
        nameColumn.DataType.ShouldBe("string");
        nameColumn.IsFilterable.ShouldBeTrue();
        nameColumn.IsSortable.ShouldBeTrue();
        nameColumn.FilterOperator.ShouldBe(TableViewFilterOperator.Contains);
        nameColumn.FilterInputHint.ShouldBe(TableViewFilterInputHint.Text);

        // Enum column. Column() auto-derives AllowedValues from the enum members, and the fluent
        // Filterable(operator) call keeps them unless a definition passes explicit values, so the
        // wire metadata serves the selectable member names to the frontend. Each label is the
        // humanized member name unless the host's catalog translates the member's convention key
        // (Enums.TestVehicleKind.{Member}), which it does for Kayak only.
        var kindColumn = res.Columns.First(c => c.Name == "Kind");
        kindColumn.DataType.ShouldBe("enum");
        kindColumn.FilterOperator.ShouldBe(TableViewFilterOperator.Enum);
        kindColumn.FilterInputHint.ShouldBe(TableViewFilterInputHint.Select);
        var kindValues = kindColumn.AllowedValues.ShouldNotBeNull();
        kindValues.Select(v => v.Value).ShouldBe(
            ["None", "Bicycle", "Scooter", "Kayak"],
            customMessage: "the auto-derived enum member names survive Filterable(operator) without explicit values");
        kindValues.Select(v => v.Label).ShouldBe(["None", "Bicycle", "Scooter", "Sea kayak"]);

        // Range and boolean columns carry the matching input hints.
        res.Columns.First(c => c.Name == "PricePerHour").FilterInputHint.ShouldBe(TableViewFilterInputHint.NumberRange);
        res.Columns.First(c => c.Name == "IsActive").FilterInputHint.ShouldBe(TableViewFilterInputHint.Select);
        res.Columns.First(c => c.Name == "AcquiredAt").FilterInputHint.ShouldBe(TableViewFilterInputHint.DateRange);

        // Temporal columns report their kind: a DateTime is a local reading, a DateTimeOffset an instant.
        res.Columns.First(c => c.Name == "AcquiredAt").DataType.ShouldBe("localDateTime");
        res.Columns.First(c => c.Name == "CreatedAt").DataType.ShouldBe("instant");

        // Display names: no translation keys are registered for these columns, so the localizer
        // serves the humanized defaults derived from the column names unchanged.
        res.Columns.First(c => c.Name == "PricePerHour").DisplayName.ShouldBe("Price Per Hour");
        res.Columns.First(c => c.Name == "IsActive").DisplayName.ShouldBe("Is Active");
        nameColumn.DisplayName.ShouldBe("Name");
    }

    [Fact]
    public async Task ExportEndpoint_FilteredExport_RowCountMatchesDataEndpointAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var filters = new Dictionary<string, string> { ["Kind"] = nameof(TestVehicleKind.Kayak) };
        IList<SortColumn> sortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }];

        // Establish the expected row count through the data endpoint with the same filter.
        var (dataRsp, dataRes) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Page = 1, PageSize = 50, Filters = filters, SortColumns = sortColumns });

        dataRsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        dataRes.TotalCount.ShouldBe(2);

        var exportResponse = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri,
            new TableViewExportRequest { Filters = filters, SortColumns = sortColumns },
            BlueprintJsonOptions.Default,
            ct);
        await exportResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        var contentType = exportResponse.Content.Headers.ContentType.ShouldNotBeNull();
        contentType.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await exportResponse.Content.ReadAsByteArrayAsync(ct);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet(1);
        worksheet.Name.ShouldBe("vehicles");

        // One header row plus one row per filtered vehicle.
        worksheet.RowsUsed().Count().ShouldBe(dataRes.TotalCount + 1);

        // Headers come from the column metadata display names, in definition order.
        worksheet.Cell(1, 1).GetString().ShouldBe("Id");
        worksheet.Cell(1, 2).GetString().ShouldBe("Name");
        worksheet.Cell(1, 3).GetString().ShouldBe("Kind");
        worksheet.Cell(1, 5).GetString().ShouldBe("Price Per Hour");
        worksheet.Cell(1, 6).GetString().ShouldBe("Is Active");

        // Data rows honour the requested sort and the enum filter, which matches on the raw member
        // name; the enum cells carry the member's localized label.
        worksheet.Cell(2, 2).GetString().ShouldBe("Family Kayak");
        worksheet.Cell(3, 2).GetString().ShouldBe("Lake Kayak");
        worksheet.Cell(2, 3).GetString().ShouldBe("Sea kayak");
        worksheet.Cell(3, 3).GetString().ShouldBe("Sea kayak");
    }

    [Fact]
    public async Task ExportEndpoint_UnpaginatedExportOverTheCap_IsRefusedAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The host's export endpoint caps an export one row below tenant A's seeded vehicles, so the
        // unfiltered export matches more rows than it may hold.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri, new TableViewExportRequest(), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");

        var content = await response.Content.ReadAsStringAsync(ct);
        content.ShouldContain($"more than the {SeedData.TenantAVehicleCount - 1} rows", Case.Sensitive);
    }

    [Fact]
    public async Task ExportEndpoint_UnpaginatedExportOfExactlyTheCap_IsExportedWholeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Every vehicle but the 25.00 Family Kayak: exactly as many rows as the host's cap. The export reads
        // one row past the cap to detect more, so a result that fills the cap must still be exported.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri,
            new TableViewExportRequest { Filters = new Dictionary<string, string> { ["PricePerHour"] = "0-20" } },
            BlueprintJsonOptions.Default,
            ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct));
        using var workbook = new XLWorkbook(stream);

        // One header row plus one row per vehicle up to the cap.
        workbook.Worksheet(1).RowsUsed().Count().ShouldBe(SeedData.TenantAVehicleCount - 1 + 1);
    }

    [Fact]
    public async Task ExportEndpoint_PageExport_IsBoundedByThePageNotTheCapAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // One page holding every seeded vehicle: more rows than the cap, which applies to unpaginated
        // exports only.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri, new TableViewExportRequest { Page = 1, PageSize = 100 }, BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct));
        using var workbook = new XLWorkbook(stream);

        // One header row plus one row per seeded vehicle.
        workbook.Worksheet(1).RowsUsed().Count().ShouldBe(SeedData.TenantAVehicleCount + 1);
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(0, 10)]
    [InlineData(1, null)]
    [InlineData(null, 10)]
    public async Task ExportEndpoint_PageOutOfBoundsOrIncomplete_IsRefusedAsync(int? page, int? pageSize)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri, new TableViewExportRequest { Page = page, PageSize = pageSize }, BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe/Atlantis")]
    public async Task DataEndpoint_TimeZoneNotIana_Returns400Async(string timeZone)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            new Uri("/api/vehicles/table-view/data", UriKind.Relative),
            new TableViewDataRequest { TimeZone = timeZone },
            BlueprintJsonOptions.Default,
            ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
        (await response.Content.ReadAsStringAsync(ct)).ShouldContain("TimeZone must be an IANA time zone id");
    }

    [Fact]
    public async Task ExportEndpoint_TimeZoneNotIana_Returns400Async()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri,
            new TableViewExportRequest { TimeZone = "Eastern Standard Time" },
            BlueprintJsonOptions.Default,
            ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task DataEndpoint_InstantDateRange_SpansDaysOfRequestZoneAsync()
    {
        // Kiritimati (+14:00) and Pago Pago (-11:00) are 25 hours apart, so any instant falls on a
        // different date in each. Filtering on the date it falls on in Kiritimati therefore finds the
        // vehicle with that zone and misses it with the other, whenever the seed ran.
        var createdAt = await this.GetCreatedAtAsync("City Bike");
        var kiritimati = FindZone("Pacific/Kiritimati");
        var date = DateOnly.FromDateTime(ZonedTime.ToLocalReading(createdAt, kiritimati)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var inKiritimati = await this.QueryNamesAsync("City Bike", date, "Pacific/Kiritimati");
        var inPagoPago = await this.QueryNamesAsync("City Bike", date, "Pacific/Pago_Pago");

        inKiritimati.ShouldBe(["City Bike"]);
        inPagoPago.ShouldBeEmpty();
    }

    [Fact]
    public async Task DataEndpoint_InstantDateRangeWithoutZone_SpansUtcDaysWhenTenantHasNoZoneAsync()
    {
        // The host registers no ITenantTimeZoneProvider, so the package's UTC fallback applies.
        var createdAt = await this.GetCreatedAtAsync("City Bike");
        var date = DateOnly.FromDateTime(createdAt.UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        (await this.QueryNamesAsync("City Bike", date, timeZone: null)).ShouldBe(["City Bike"]);
    }

    [Fact]
    public async Task ExportEndpoint_WritesTemporalValuesAsDateCellsInRequestZoneAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdAt = await this.GetCreatedAtAsync("City Bike");

        var exportResponse = await this.App.ManagerAClient.PostAsJsonAsync(
            ExportUri,
            new TableViewExportRequest { Filters = new Dictionary<string, string> { ["Name"] = "City Bike" }, TimeZone = "Pacific/Kiritimati" },
            BlueprintJsonOptions.Default,
            ct);
        await exportResponse.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        using var stream = new MemoryStream(await exportResponse.Content.ReadAsByteArrayAsync(ct));
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        // Columns follow the definition: ..., AcquiredAt (local reading), CreatedAt (instant). Their headers
        // name their zones: the tenant's for the reading (UTC, as the host registers no provider), the
        // requested one for the instant.
        worksheet.Cell(1, 7).GetString().ShouldBe("Acquired At (UTC)");
        worksheet.Cell(2, 7).DataType.ShouldBe(XLDataType.DateTime);
        worksheet.Cell(2, 7).GetDateTime().ShouldBe(new DateTime(2023, 3, 15, 0, 0, 0, DateTimeKind.Unspecified));

        worksheet.Cell(1, 8).GetString().ShouldBe("Created At (Pacific/Kiritimati)");
        worksheet.Cell(2, 8).DataType.ShouldBe(XLDataType.DateTime);
        var expected = ZonedTime.ToLocalReading(createdAt, FindZone("Pacific/Kiritimati"));
        worksheet.Cell(2, 8).GetDateTime().ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private async Task<DateTimeOffset> GetCreatedAtAsync(string name)
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest { Filters = new Dictionary<string, string> { ["Name"] = name } });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        return res.Data.Single(v => v.Name == name).CreatedAt;
    }

    private async Task<List<string>> QueryNamesAsync(string name, string date, string? timeZone)
    {
        var (rsp, res) = await this.App.ManagerAClient
            .POSTAsync<VehicleTableViewDataEndpoint, TableViewDataRequest, TableViewDataResponse<VehicleTableViewDto>>(
                new TableViewDataRequest
                {
                    Filters = new Dictionary<string, string> { ["Name"] = name, ["CreatedAt"] = $"{date},{date}" },
                    TimeZone = timeZone,
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        return res.Data.Select(v => v.Name).Where(n => n == name).ToList();
    }
}
