namespace Csag.Blueprint.IntegrationTests.TableView;

using System.Globalization;
using ClosedXML.Excel;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A table view over time values on SQL Server: each member is a flat column of the DTO
/// (<c>Column(dto => dto.StartLocal, e => e.Start.Local)</c>), with the data type of its kind, and filters,
/// sorts and exports like a column over a plain property.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TimeValueTableViewTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly TimeZoneInfo Zurich = FindZone("Europe/Zurich");

    [Fact]
    public void Metadata_MemberColumns_HaveTheDataTypesOfTheirKinds()
    {
        var metadata = new AppointmentTableViewDefinition().Metadata.ToDictionary(c => c.Name);

        metadata[nameof(AppointmentTableViewDto.StartLocal)].DataType.ShouldBe(TableViewDataTypes.LocalDateTime);
        metadata[nameof(AppointmentTableViewDto.StartLocal)].TimeZoneColumn.ShouldBeNull();
        metadata[nameof(AppointmentTableViewDto.StartAt)].DataType.ShouldBe(TableViewDataTypes.Instant);
        metadata[nameof(AppointmentTableViewDto.StandOpensLocal)].DataType.ShouldBe(TableViewDataTypes.LocalDateTime);
        metadata[nameof(AppointmentTableViewDto.StandOpensLocal)].TimeZoneColumn.ShouldBe(nameof(AppointmentTableViewDto.StandOpensTimeZoneId));
        metadata[nameof(AppointmentTableViewDto.StandOpensAt)].DataType.ShouldBe(TableViewDataTypes.Instant);
    }

    [Fact]
    public async Task DateRange_OnTheReading_MatchesTheDayAsItReadsAsync()
    {
        await this.SeedAsync();

        var titles = await this.QueryTitlesAsync(new Dictionary<string, string> { [nameof(AppointmentTableViewDto.StartLocal)] = "2026-10-25,2026-10-25" }, sortColumn: null, timeZone: null);

        titles.ShouldBe(["Morning", "Night"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("Europe/Zurich", new[] { "Morning", "Night" })]
    [InlineData("UTC", new[] { "Morning" })]
    public async Task DateRange_OnTheInstant_SpansTheDayInTheRequestZoneAsync(string timeZone, string[] expected)
    {
        await this.SeedAsync();

        var titles = await this.QueryTitlesAsync(new Dictionary<string, string> { [nameof(AppointmentTableViewDto.StartAt)] = "2026-10-25,2026-10-25" }, sortColumn: null, FindZone(timeZone));

        titles.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task DateRange_OnAReadingWithItsOwnZone_MatchesTheDayAsItReadsAsync()
    {
        await this.SeedAsync();

        var titles = await this.QueryTitlesAsync(new Dictionary<string, string> { [nameof(AppointmentTableViewDto.StandOpensLocal)] = "2026-11-14,2026-11-14" }, sortColumn: null, timeZone: null);

        titles.ShouldBe(["Morning", "Night", "Late"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(nameof(AppointmentTableViewDto.StartAt), new[] { "Late", "Night", "Morning" })]
    [InlineData(nameof(AppointmentTableViewDto.StartLocal), new[] { "Late", "Night", "Morning" })]
    [InlineData(nameof(AppointmentTableViewDto.StandOpensLocal), new[] { "Late", "Morning", "Night" })] // 09:00, 10:00, 11:00 as they read
    [InlineData(nameof(AppointmentTableViewDto.StandOpensAt), new[] { "Night", "Morning", "Late" })] // London, New York, Los Angeles
    public async Task Sort_OnAMemberColumn_OrdersByItAsync(string column, string[] expected)
    {
        await this.SeedAsync();

        (await this.QueryTitlesAsync(new Dictionary<string, string>(), column, timeZone: null)).ShouldBe(expected);
    }

    [Fact]
    public async Task Export_WritesReadingsAsTheyReadAndInstantsInTheRequestZoneAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await this.SeedAsync();
        var newYork = FindZone("America/New_York");
        var definition = new AppointmentTableViewDefinition();

        using var scope = this.App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        TenantContext.SetTenant(SeedData.TenantAId);
        var rows = await scope.ServiceProvider.GetRequiredService<ITableViewExecutor>().ExecuteWithoutPaginationAsync(
            context.Appointments,
            definition,
            new Dictionary<string, string> { [nameof(AppointmentTableViewDto.Title)] = "Night" },
            sortColumns: null,
            newYork,
            ct);

        var workbook = TableViewExcelExporter.Export(rows, definition.Metadata, timeZone: newYork, placeTimeZone: Zurich, culture: CultureInfo.InvariantCulture);

        using var stream = new MemoryStream(workbook);
        using var excel = new XLWorkbook(stream);
        var sheet = excel.Worksheet(1);
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        headers.ShouldBe(
        [
            "Title",
            "Starts (Europe/Zurich)",
            "Starts at (America/New_York)",
            "Stand opens",
            "Stand zone",
            "Stand opens at (America/New_York)",
        ]);

        // Night starts at 00:30 on the 25th in Zurich, which is 18:30 on the 24th in New York.
        sheet.Cell(2, 2).GetDateTime().ShouldBe(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Unspecified));
        sheet.Cell(2, 3).GetDateTime().ShouldBe(new DateTime(2026, 10, 24, 18, 30, 0, DateTimeKind.Unspecified));

        // Its stand opens at 11:00 in London, as it reads there, and at 06:00 in New York.
        sheet.Cell(2, 4).GetDateTime().ShouldBe(new DateTime(2026, 11, 14, 11, 0, 0, DateTimeKind.Unspecified));
        sheet.Cell(2, 5).GetString().ShouldBe("Europe/London");
        sheet.Cell(2, 6).GetDateTime().ShouldBe(new DateTime(2026, 11, 14, 6, 0, 0, DateTimeKind.Unspecified));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private static TestAppointment NewAppointment(string title, DateTime start, DateTime standOpens, string standTimeZoneId)
    {
        return new TestAppointment
        {
            Id = Guid.NewGuid(),
            TenantId = SeedData.TenantAId,
            Title = title,
            Start = PlaceTime.Create(start, Zurich),
            StandOpens = ZonedPlaceTime.Create(standOpens, standTimeZoneId),
            Briefing = ViewerTime.Create(start, "Europe/Zurich"),
        };
    }

    private async Task<List<string>> QueryTitlesAsync(Dictionary<string, string> filters, string? sortColumn, TimeZoneInfo? timeZone)
    {
        using var scope = this.App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        TenantContext.SetTenant(SeedData.TenantAId);

        var (data, _) = await scope.ServiceProvider.GetRequiredService<ITableViewExecutor>().ExecuteAsync(
            new TableViewQueryRequest<TestAppointment, AppointmentTableViewDto>
            {
                Query = context.Appointments,
                Definition = new AppointmentTableViewDefinition(),
                Filters = filters,
                SortColumns = sortColumn is null ? null : [new SortColumn { ColumnName = sortColumn, Direction = SortDirection.Asc }],
                TimeZone = timeZone,
                PageSize = 50,
            },
            TestContext.Current.CancellationToken);

        return data.Select(row => row.Title).ToList();
    }

    /// <summary>
    /// Seeds three appointments in Zurich. Night starts on the 25th in Zurich but on the 24th in UTC; Late
    /// starts on the 24th in both. Their stands open on 2026-11-14 in three zones, so their readings and
    /// instants sort differently.
    /// </summary>
    private async Task SeedAsync()
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        scope.Context.Appointments.AddRange(
            NewAppointment("Morning", new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York"),
            NewAppointment("Night", new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Unspecified), new DateTime(2026, 11, 14, 11, 0, 0, DateTimeKind.Unspecified), "Europe/London"),
            NewAppointment("Late", new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Unspecified), new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Unspecified), "America/Los_Angeles"));
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class AppointmentTableViewDefinition : TableViewDefinition<TestAppointment, AppointmentTableViewDto>
    {
        public AppointmentTableViewDefinition()
        {
            this.Column(dto => dto.Title, e => e.Title)
                .Filterable(TableViewFilterOperator.Equals)
                .WithDisplayName("Title");

            this.Column(dto => dto.StartLocal, e => e.Start.Local)
                .Filterable(TableViewFilterOperator.DateRange)
                .Sortable()
                .WithDisplayName("Starts");

            this.Column(dto => dto.StartAt, e => e.Start.At)
                .Filterable(TableViewFilterOperator.DateRange)
                .Sortable()
                .WithDisplayName("Starts at");

            this.Column(dto => dto.StandOpensLocal, e => e.StandOpens.Local)
                .Filterable(TableViewFilterOperator.DateRange)
                .Sortable()
                .WithTimeZoneFrom(dto => dto.StandOpensTimeZoneId)
                .WithDisplayName("Stand opens");

            this.Column(dto => dto.StandOpensTimeZoneId, e => e.StandOpens.TimeZoneId)
                .WithDisplayName("Stand zone");

            this.Column(dto => dto.StandOpensAt, e => e.StandOpens.At)
                .Sortable()
                .WithDisplayName("Stand opens at");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S3459:Unassigned members should be removed", Justification = "Assigned by the table view projection, which binds the properties through reflection.")]
    private sealed class AppointmentTableViewDto
    {
        public string Title { get; set; } = string.Empty;

        public DateTime StartLocal { get; set; }

        public DateTimeOffset StartAt { get; set; }

        public DateTime StandOpensLocal { get; set; }

        public string StandOpensTimeZoneId { get; set; } = string.Empty;

        public DateTimeOffset StandOpensAt { get; set; }
    }
}
