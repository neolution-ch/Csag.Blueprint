namespace Csag.Blueprint.Web.UnitTests.TableView;

using ClosedXML.Excel;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.TableView;

/// <summary>
/// Unit tests for <see cref="TableViewExcelExporter"/>: cells whose value is one of the column's
/// allowed values are written as that value's label, everything else as the raw value.
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

    private sealed class Row
    {
        public TestVehicleKind Kind { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
