namespace Csag.Blueprint.Web.UnitTests.TableView;

using System.Globalization;
using ClosedXML.Excel;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;

/// <summary>
/// Unit tests for <see cref="TableViewExcelExporter"/>: cells whose value is one of the column's
/// allowed values are written as that value's label, everything else as the raw value; temporal values
/// are written as date cells, instants in the given zone. Every test names its culture and zone, so
/// nothing depends on the machine's own settings.
/// </summary>
public sealed class TableViewExcelExporterTests
{
    [Fact]
    public void Export_EnumCellMatchingAnAllowedValue_WritesItsLabel()
    {
        // Arrange
        IList<TableViewColumnMetadata> metadata =
        [
            new()
            {
                Name = nameof(Row.Kind),
                DisplayName = "Kind",
                DataType = "enum",
                AllowedValues = [new("Bicycle", "Bicycle"), new("Kayak", "Sea kayak")],
            },
            new() { Name = nameof(Row.Name), DisplayName = "Name", DataType = "string" },
        ];
        IList<Row> data =
        [
            new() { Kind = TestVehicleKind.Kayak, Name = "Kayak" },
            new() { Kind = TestVehicleKind.Scooter, Name = "Vespa" },
        ];

        // Act
        using var workbook = new XLWorkbook(new MemoryStream(TableViewExcelExporter.Export(data, metadata)));

        // Assert
        var worksheet = workbook.Worksheet(1);
        worksheet.Cell(2, 1).GetString().ShouldBe("Sea kayak");
        worksheet.Cell(3, 1).GetString().ShouldBe("Scooter", customMessage: "a value outside the allowed values is written raw");
        worksheet.Cell(2, 2).GetString().ShouldBe("Kayak", customMessage: "a column without allowed values is written raw");
    }

    [Fact]
    public void Export_TemporalValues_AreDateCellsInZoneWithFormatsOfCulture()
    {
        // Arrange — 23:30Z on 25 October 2026 is 00:30 on the 26th in Zurich (+01:00, after the fall-back).
        var data = new List<TemporalRow>
        {
            new()
            {
                PurchaseDate = new DateOnly(2026, 10, 21),
                StartLocal = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified),
                CreatedAt = new DateTimeOffset(2026, 10, 25, 23, 30, 15, TimeSpan.Zero),
            },
        };

        // Act
        var worksheet = ExportTemporalRows(data, FindZone("Europe/Zurich"), CultureInfo.GetCultureInfo("de-CH"));

        // Assert
        var date = worksheet.Cell(2, 1);
        date.DataType.ShouldBe(XLDataType.DateTime);
        date.GetDateTime().ShouldBe(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Unspecified));
        date.Style.DateFormat.Format.ShouldBe("dd.mm.yyyy");

        var localReading = worksheet.Cell(2, 2);
        localReading.DataType.ShouldBe(XLDataType.DateTime);
        localReading.GetDateTime().ShouldBe(new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Unspecified), "a local reading is written as it reads");
        localReading.Style.DateFormat.Format.ShouldBe("dd.mm.yyyy hh:mm");

        var instant = worksheet.Cell(2, 3);
        instant.DataType.ShouldBe(XLDataType.DateTime);
        instant.GetDateTime().ShouldBe(new DateTime(2026, 10, 26, 0, 30, 15, DateTimeKind.Unspecified), "an instant is written as a clock in the zone showed it");
        instant.Style.DateFormat.Format.ShouldBe("dd.mm.yyyy hh:mm:ss");
    }

    [Fact]
    public void Export_Instant_FollowsZone()
    {
        var data = new List<TemporalRow> { new() { CreatedAt = new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero) } };

        var inNewYork = ExportTemporalRows(data, FindZone("America/New_York"), CultureInfo.InvariantCulture);
        var inUtc = ExportTemporalRows(data, timeZone: null, CultureInfo.InvariantCulture);

        inNewYork.Cell(2, 3).GetDateTime().ShouldBe(new DateTime(2026, 10, 25, 19, 30, 0, DateTimeKind.Unspecified));
        inUtc.Cell(2, 3).GetDateTime().ShouldBe(new DateTime(2026, 10, 25, 23, 30, 0, DateTimeKind.Unspecified), "instants are shown in UTC when no zone is given");
    }

    [Fact]
    public void Export_CultureWithTwelveHourClock_UsesItsOrderAndAmPm()
    {
        var data = new List<TemporalRow>
        {
            new()
            {
                PurchaseDate = new DateOnly(2026, 10, 21),
                StartLocal = new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Unspecified),
                CreatedAt = new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero),
            },
        };

        var worksheet = ExportTemporalRows(data, TimeZoneInfo.Utc, CultureInfo.GetCultureInfo("en-US"));

        worksheet.Cell(2, 1).Style.DateFormat.Format.ShouldBe("m/d/yyyy");
        worksheet.Cell(2, 2).Style.DateFormat.Format.ShouldStartWith("m/d/yyyy h:mm");
        worksheet.Cell(2, 2).Style.DateFormat.Format.ShouldEndWith("AM/PM");
        worksheet.Cell(2, 3).Style.DateFormat.Format.ShouldStartWith("m/d/yyyy h:mm:ss");
    }

    // Every case sets its calendar, because a culture's default calendar comes from the host's ICU data:
    // ICU 74 gives ar-SA Um al-Qura, ICU 78 the Gregorian calendar.
    [Theory]
    [InlineData("th-TH", typeof(ThaiBuddhistCalendar), "[$-1070000]")]
    [InlineData("ar-SA", typeof(UmAlQuraCalendar), "[$-1170000]")]
    [InlineData("fa-IR", typeof(PersianCalendar), "[$-1160000]")]
    [InlineData("ar-SA", typeof(HijriCalendar), "[$-1060000]")]
    [InlineData("ja-JP", typeof(JapaneseCalendar), "[$-1030000]")]
    [InlineData("ko-KR", typeof(KoreanCalendar), "[$-1050000]")]
    [InlineData("he-IL", typeof(HebrewCalendar), "[$-1080000]")]
    public void Export_CultureWithAnotherCalendar_FormatsDatesInThatCalendar(string cultureName, Type calendarType, string calendarCode)
    {
        var culture = WithCalendar(cultureName, calendarType);
        var data = new List<TemporalRow>
        {
            new()
            {
                PurchaseDate = new DateOnly(2026, 10, 21),
                StartLocal = new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Unspecified),
                CreatedAt = new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero),
            },
        };

        var worksheet = ExportTemporalRows(data, TimeZoneInfo.Utc, culture);

        worksheet.Cell(2, 1).GetDateTime().ShouldBe(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Unspecified), "the cell holds the date; its format picks the calendar");
        worksheet.Cell(2, 1).Style.DateFormat.Format.ShouldStartWith(calendarCode);
        worksheet.Cell(2, 2).Style.DateFormat.Format.ShouldStartWith(calendarCode);
        worksheet.Cell(2, 3).Style.DateFormat.Format.ShouldStartWith(calendarCode);
    }

    [Theory]
    [InlineData("de-CH", null)]
    [InlineData("zh-TW", typeof(TaiwanCalendar))] // Excel shows Taiwan years as Gregorian ones
    public void Export_GregorianOrUnsupportedCalendar_FormatsDatesWithoutCalendarCode(string cultureName, Type? calendarType)
    {
        var data = new List<TemporalRow> { new() { PurchaseDate = new DateOnly(2026, 10, 21) } };

        var worksheet = ExportTemporalRows(data, TimeZoneInfo.Utc, WithCalendar(cultureName, calendarType));

        worksheet.Cell(2, 1).Style.DateFormat.Format.ShouldNotStartWith("[");
    }

    [Fact]
    public void Export_NullAndDefaultTemporalValues_AreEmptyCells()
    {
        var data = new List<TemporalRow> { new() { PurchaseDate = default, StartLocal = default, CreatedAt = null } };

        var worksheet = ExportTemporalRows(data, TimeZoneInfo.Utc, CultureInfo.InvariantCulture);

        worksheet.Cell(2, 1).GetString().ShouldBeEmpty();
        worksheet.Cell(2, 2).GetString().ShouldBeEmpty();
        worksheet.Cell(2, 3).GetString().ShouldBeEmpty();
    }

    [Fact]
    public void Export_ColumnsWithATime_NameTheirZoneInTheHeader()
    {
        var data = new List<TemporalRow> { new() { PurchaseDate = new DateOnly(2026, 10, 21) } };

        var worksheet = ExportTemporalRows(data, FindZone("Europe/London"), CultureInfo.InvariantCulture, FindZone("Europe/Zurich"));

        worksheet.Cell(1, 1).GetString().ShouldBe("Purchase date", "a date means the same in every zone");
        worksheet.Cell(1, 2).GetString().ShouldBe("Start (Europe/Zurich)", "a local reading names the place zone");
        worksheet.Cell(1, 3).GetString().ShouldBe("Created (Europe/London)", "an instant names the zone it is shown in");
    }

    [Fact]
    public void Export_WithoutZones_NamesUtcForInstantsAndNoZoneForLocalReadings()
    {
        var data = new List<TemporalRow> { new() };

        var worksheet = ExportTemporalRows(data, timeZone: null, CultureInfo.InvariantCulture);

        worksheet.Cell(1, 2).GetString().ShouldBe("Start");
        worksheet.Cell(1, 3).GetString().ShouldBe("Created (UTC)");
    }

    [Fact]
    public void Export_LocalReadingColumnWithZoneFromRow_NamesNoZoneAndKeepsReadings()
    {
        // Arrange — a Zurich tenant's stands, one of them at a boat show in Miami.
        IList<TableViewColumnMetadata> metadata =
        [
            new() { Name = nameof(StandRow.OpensLocal), DisplayName = "Opens", DataType = "localDateTime", TimeZoneColumn = nameof(StandRow.TimeZoneId) },
            new() { Name = nameof(StandRow.ClosesLocal), DisplayName = "Closes", DataType = "localDateTime" },
            new() { Name = nameof(StandRow.TimeZoneId), DisplayName = "Time zone", DataType = "string" },
        ];
        IList<StandRow> data =
        [
            new() { OpensLocal = new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Unspecified), ClosesLocal = new DateTime(2026, 10, 25, 18, 0, 0, DateTimeKind.Unspecified), TimeZoneId = "Europe/Zurich" },
            new() { OpensLocal = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Unspecified), ClosesLocal = new DateTime(2026, 10, 25, 17, 0, 0, DateTimeKind.Unspecified), TimeZoneId = "America/New_York" },
        ];

        // Act
        var bytes = TableViewExcelExporter.Export(data, metadata, timeZone: FindZone("Europe/London"), placeTimeZone: FindZone("Europe/Zurich"), culture: CultureInfo.InvariantCulture);

        // Assert
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet(1);
        worksheet.Cell(1, 1).GetString().ShouldBe("Opens", "its rows are in different zones, which the zone column carries");
        worksheet.Cell(1, 2).GetString().ShouldBe("Closes (Europe/Zurich)", "a local reading without a row zone is in the tenant zone");
        worksheet.Cell(1, 3).GetString().ShouldBe("Time zone");
        worksheet.Cell(3, 1).GetDateTime().ShouldBe(new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Unspecified), "the reading is written as it reads");
        worksheet.Cell(3, 3).GetString().ShouldBe("America/New_York");
    }

    [Fact]
    public void Export_NonTemporalColumn_KeepsItsHeader()
    {
        IList<TableViewColumnMetadata> metadata = [new() { Name = nameof(Row.Name), DisplayName = "Name", DataType = "string" }];

        var bytes = TableViewExcelExporter.Export(new List<Row> { new() { Name = "Vespa" } }, metadata, timeZone: FindZone("Europe/Zurich"), placeTimeZone: FindZone("Europe/Zurich"));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheet(1).Cell(1, 1).GetString().ShouldBe("Name");
    }

    private static IXLWorksheet ExportTemporalRows(IList<TemporalRow> data, TimeZoneInfo? timeZone, CultureInfo culture, TimeZoneInfo? placeTimeZone = null)
    {
        IList<TableViewColumnMetadata> metadata =
        [
            new() { Name = nameof(TemporalRow.PurchaseDate), DisplayName = "Purchase date", DataType = "date" },
            new() { Name = nameof(TemporalRow.StartLocal), DisplayName = "Start", DataType = "localDateTime" },
            new() { Name = nameof(TemporalRow.CreatedAt), DisplayName = "Created", DataType = "instant" },
        ];

        var bytes = TableViewExcelExporter.Export(data, metadata, "Export", timeZone: timeZone, placeTimeZone: placeTimeZone, culture: culture);
        var workbook = new XLWorkbook(new MemoryStream(bytes));
        return workbook.Worksheet(1);
    }

    private static CultureInfo WithCalendar(string cultureName, Type? calendarType)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        if (calendarType is not null)
        {
            culture.DateTimeFormat.Calendar = (Calendar)Activator.CreateInstance(calendarType)!;
        }

        return culture;
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private sealed class Row
    {
        public TestVehicleKind Kind { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class StandRow
    {
        public DateTime OpensLocal { get; init; }

        public DateTime ClosesLocal { get; init; }

        public string TimeZoneId { get; init; } = string.Empty;
    }

    private sealed class TemporalRow
    {
        public DateOnly PurchaseDate { get; init; }

        public DateTime StartLocal { get; init; }

        public DateTimeOffset? CreatedAt { get; init; }
    }
}
