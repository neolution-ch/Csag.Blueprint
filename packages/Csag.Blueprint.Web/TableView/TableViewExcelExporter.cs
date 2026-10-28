namespace Csag.Blueprint.Web.TableView;

using System.Globalization;
using ClosedXML.Excel;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Time;

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
    /// <remarks>
    /// Temporal values are written as Excel date cells, so they sort and filter as dates: a
    /// <see cref="DateOnly"/> as a date, a local reading (<see cref="DateTime"/>) as the date and time it
    /// reads, and an instant (<see cref="DateTimeOffset"/>) as the date and time a clock in
    /// <paramref name="timeZone"/> showed at that instant. Their number formats follow the short date and
    /// time patterns of <paramref name="culture"/>; instants also show seconds.
    /// <para>
    /// A cell with a time means nothing without its zone, so the headers of those columns name it, as
    /// the IANA id after the display name: <c>Created at (Europe/London)</c>. An instant column names
    /// <paramref name="timeZone"/>, a local reading column names <paramref name="placeTimeZone"/>, and a
    /// date column names none. A local reading column whose zone comes from its row
    /// (<see cref="TableViewColumnMetadata.TimeZoneColumn"/>) names none either: its rows can be in different
    /// zones, which the zone column carries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="data">The data rows to export.</param>
    /// <param name="metadata">The column metadata defining headers, data types, and value labels.</param>
    /// <param name="sheetName">The worksheet name.</param>
    /// <param name="timeZone">The zone instants are shown in; UTC when null.</param>
    /// <param name="placeTimeZone">
    /// The zone of the place whose clock the local readings come from, normally the tenant's zone; when null,
    /// local reading headers name no zone.
    /// </param>
    /// <param name="culture">The culture whose date and time patterns format the cells; the current culture when null.</param>
    /// <returns>The Excel file as a byte array.</returns>
    public static byte[] Export<TDto>(
        IList<TDto> data,
        IList<TableViewColumnMetadata> metadata,
        string sheetName = "Export",
        TimeZoneInfo? timeZone = null,
        TimeZoneInfo? placeTimeZone = null,
        CultureInfo? culture = null)
        where TDto : class
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(metadata);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        var properties = typeof(TDto).GetProperties();
        var temporalFormats = ExcelTemporalFormats.For(culture ?? CultureInfo.CurrentCulture);
        var zone = timeZone ?? TimeZoneInfo.Utc;

        // Write headers from metadata display names
        for (var col = 0; col < metadata.Count; col++)
        {
            var headerCell = worksheet.Cell(1, col + 1);
            headerCell.Value = GetHeader(metadata[col], zone, placeTimeZone);
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

                SetCellValue(cell, value, columnMeta.DataType, valueLabels[col], zone, temporalFormats);
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetHeader(TableViewColumnMetadata column, TimeZoneInfo timeZone, TimeZoneInfo? placeTimeZone)
    {
        TimeZoneInfo? headerZone = null;
        if (column.DataType == TableViewDataTypes.Instant)
        {
            headerZone = timeZone;
        }
        else if (column.DataType == TableViewDataTypes.LocalDateTime && column.TimeZoneColumn is null)
        {
            headerZone = placeTimeZone;
        }
        else
        {
            // A date or a column without a time means the same in every zone, and a local reading whose
            // zone comes from its row has no single zone to name.
        }

        return headerZone is null ? column.DisplayName : $"{column.DisplayName} ({headerZone.Id})";
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

    private static void SetCellValue(
        IXLCell cell,
        object? value,
        string dataType,
        Dictionary<string, string>? valueLabels,
        TimeZoneInfo timeZone,
        ExcelTemporalFormats temporalFormats)
    {
        if (value == null)
        {
            cell.SetValue(string.Empty);
            return;
        }

        if (dataType == TableViewDataTypes.Number)
        {
            SetNumericCellValue(cell, value);
        }
        else if (dataType == TableViewDataTypes.Boolean)
        {
            cell.SetValue(value is true ? "Yes" : "No");
        }
        else if (value is DateOnly or DateTime or DateTimeOffset)
        {
            SetTemporalCellValue(cell, value, timeZone, temporalFormats);
        }
        else
        {
            cell.SetValue(ToCellText(value, valueLabels));
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
            cell.SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a temporal value as an Excel date cell. A value in year 1 is the CLR default and is written
    /// as an empty cell; Excel cannot represent dates before 1900 anyway.
    /// </summary>
    private static void SetTemporalCellValue(IXLCell cell, object value, TimeZoneInfo timeZone, ExcelTemporalFormats temporalFormats)
    {
        var (reading, format) = value switch
        {
            DateOnly date => (date.ToDateTime(TimeOnly.MinValue), temporalFormats.Date),
            DateTime localReading => (DateTime.SpecifyKind(localReading, DateTimeKind.Unspecified), temporalFormats.LocalDateTime),
            DateTimeOffset instant when instant.UtcDateTime.Year > 1 => (ZonedTime.ToLocalReading(instant, timeZone), temporalFormats.Instant),
            _ => (DateTime.MinValue, string.Empty),
        };

        if (reading.Year <= 1)
        {
            cell.SetValue(string.Empty);
            return;
        }

        cell.SetValue(reading);
        cell.Style.DateFormat.Format = format;
    }
}
