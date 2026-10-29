namespace Csag.Blueprint.Web.TableView;

using ClosedXML.Excel;
using Csag.Blueprint.Application.TableView;

/// <summary>
/// Generates Excel files from table view data using column metadata for headers and formatting.
/// </summary>
public static class TableViewExcelExporter
{
    /// <summary>
    /// Exports the given data to an Excel workbook as a byte array. A text or enum cell whose value
    /// matches one of the column's <see cref="TableViewColumnMetadata.AllowedValues"/> is written as
    /// that allowed value's label, so pass localized metadata to get localized labels.
    /// </summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="data">The data rows to export.</param>
    /// <param name="metadata">The column metadata defining headers, data types, and value labels.</param>
    /// <param name="sheetName">The worksheet name.</param>
    /// <returns>The Excel file as a byte array.</returns>
    public static byte[] Export<TDto>(IList<TDto> data, IList<TableViewColumnMetadata> metadata, string sheetName = "Export")
        where TDto : class
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        var properties = typeof(TDto).GetProperties();

        // Write headers from metadata display names
        for (var col = 0; col < metadata.Count; col++)
        {
            var headerCell = worksheet.Cell(1, col + 1);
            headerCell.Value = metadata[col].DisplayName;
            headerCell.Style.Font.Bold = true;
        }

        // Write data rows
        var valueLabels = metadata.Select(CreateValueLabelLookup).ToArray();
        for (var row = 0; row < data.Count; row++)
        {
            var item = data[row];
            for (var col = 0; col < metadata.Count; col++)
            {
                var columnMeta = metadata[col];
                var prop = FindProperty(properties, columnMeta.Name);
                var value = prop?.GetValue(item);
                var cell = worksheet.Cell(row + 2, col + 1);

                SetCellValue(cell, value, columnMeta.DataType, valueLabels[col]);
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static System.Reflection.PropertyInfo? FindProperty(
        System.Reflection.PropertyInfo[] properties,
        string columnName)
    {
        // Match property by name (case-insensitive)
        return Array.Find(properties, p => string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string>? CreateValueLabelLookup(TableViewColumnMetadata column)
    {
        if (column.AllowedValues is not { Count: > 0 } allowedValues)
        {
            return null;
        }

        var lookup = new Dictionary<string, string>(allowedValues.Count, StringComparer.Ordinal);
        foreach (var allowedValue in allowedValues)
        {
            lookup.TryAdd(allowedValue.Value, allowedValue.Label);
        }

        return lookup;
    }

    private static string? ToCellText(object value, Dictionary<string, string>? valueLabels)
    {
        var text = value.ToString();
        return text is not null && valueLabels is not null && valueLabels.TryGetValue(text, out var label)
            ? label
            : text;
    }

    private static void SetCellValue(IXLCell cell, object? value, string dataType, Dictionary<string, string>? valueLabels)
    {
        if (value == null)
        {
            cell.SetValue(string.Empty);
            return;
        }

        switch (dataType)
        {
            case "number":
                SetNumericCellValue(cell, value);
                break;

            case "boolean":
                cell.SetValue(value is true ? "Yes" : "No");
                break;

            case "date":
                SetDateCellValue(cell, value);
                break;

            default:
                cell.SetValue(ToCellText(value, valueLabels));
                break;
        }
    }

    private static void SetNumericCellValue(IXLCell cell, object value)
    {
        if (value is decimal d)
        {
            cell.SetValue(d);
        }
        else if (value is double dbl)
        {
            cell.SetValue(dbl);
        }
        else if (value is int i)
        {
            cell.SetValue(i);
        }
        else if (value is long l)
        {
            cell.SetValue(l);
        }
        else
        {
            cell.SetValue(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static void SetDateCellValue(IXLCell cell, object value)
    {
        if (value is DateOnly dateOnly)
        {
            if (dateOnly.Year <= 1)
            {
                cell.SetValue(string.Empty);
            }
            else
            {
                cell.SetValue(dateOnly.ToDateTime(TimeOnly.MinValue));
                cell.Style.DateFormat.Format = "dd.MM.yyyy";
            }
        }
        else if (value is DateTime dt)
        {
            if (dt.Year <= 1)
            {
                cell.SetValue(string.Empty);
            }
            else
            {
                cell.SetValue(dt);
                cell.Style.DateFormat.Format = "dd.MM.yyyy";
            }
        }
        else if (value is DateTimeOffset dto)
        {
            if (dto.Year <= 1)
            {
                cell.SetValue(string.Empty);
            }
            else
            {
                cell.SetValue(new DateTime(dto.Year, dto.Month, dto.Day, 0, 0, 0, DateTimeKind.Utc));
                cell.Style.DateFormat.Format = "dd.MM.yyyy";
            }
        }
        else
        {
            cell.SetValue(value.ToString());
        }
    }
}
