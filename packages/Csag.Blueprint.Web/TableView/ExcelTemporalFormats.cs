namespace Csag.Blueprint.Web.TableView;

using System.Globalization;
using System.Text;

/// <summary>
/// Excel number formats for date cells, built from a culture's .NET date and time patterns so an export
/// reads the way the UI formats dates for the same language, whatever the regional settings of the
/// machine that opens it.
/// </summary>
/// <param name="Date">The format of a calendar date.</param>
/// <param name="LocalDateTime">The format of a local reading: date and time to the minute.</param>
/// <param name="Instant">The format of an instant: date and time to the second.</param>
internal sealed record ExcelTemporalFormats(string Date, string LocalDateTime, string Instant)
{
    /// <summary>
    /// Characters Excel shows as themselves in a number format without quoting.
    /// </summary>
    private const string PlainLiterals = " -/:.,()";

    /// <summary>
    /// .NET specifiers Excel writes the same way: day, year, second, 12-hour hour and minute.
    /// </summary>
    private const string KeptSpecifiers = "dyshm";

    /// <summary>
    /// .NET specifiers Excel has no equivalent for: fractions, era and offsets.
    /// </summary>
    private const string DroppedSpecifiers = "fFgzK";

    /// <summary>
    /// Builds the formats for a culture from its short date, short time and long time patterns.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The Excel number formats.</returns>
    public static ExcelTemporalFormats For(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var patterns = culture.DateTimeFormat;
        var date = ToExcelFormat(patterns.ShortDatePattern, patterns);
        return new ExcelTemporalFormats(
            date,
            $"{date} {ToExcelFormat(patterns.ShortTimePattern, patterns)}",
            $"{date} {ToExcelFormat(patterns.LongTimePattern, patterns)}");
    }

    /// <summary>
    /// Translates a .NET custom date and time pattern into an Excel number format. Excel reads <c>m</c> as
    /// minutes next to hours or seconds and as the month elsewhere, picks a 12-hour clock only when the format
    /// has an AM/PM marker, and has no equivalent for eras, fractions or offsets, which are dropped.
    /// </summary>
    private static string ToExcelFormat(string pattern, DateTimeFormatInfo patterns)
    {
        var format = new StringBuilder(pattern.Length + 8);
        var index = 0;
        while (index < pattern.Length)
        {
            index += AppendToken(format, pattern, index, patterns);
        }

        return format.ToString().Trim();
    }

    /// <summary>
    /// Appends the Excel form of the pattern token at <paramref name="index"/>.
    /// </summary>
    /// <returns>The number of pattern characters the token spans.</returns>
    private static int AppendToken(StringBuilder format, string pattern, int index, DateTimeFormatInfo patterns)
    {
        var current = pattern[index];
        if (current is '\'' or '"')
        {
            var end = pattern.IndexOf(current, index + 1);
            end = end < 0 ? pattern.Length : end;
            format.Append(Escape(pattern[(index + 1)..end]));
            return end - index + 1;
        }

        if (current == '\\')
        {
            format.Append(index + 1 < pattern.Length ? Escape(pattern.Substring(index + 1, 1)) : string.Empty);
            return 2;
        }

        var run = 1;
        while (index + run < pattern.Length && pattern[index + run] == current)
        {
            run++;
        }

        format.Append(TranslateRun(current, run, patterns));
        return run;
    }

    private static string TranslateRun(char specifier, int run, DateTimeFormatInfo patterns)
    {
        if (KeptSpecifiers.Contains(specifier, StringComparison.Ordinal))
        {
            return new string(specifier, run);
        }

        if (DroppedSpecifiers.Contains(specifier, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return specifier switch
        {
            'M' => new string('m', run),
            'H' => new string('h', run),
            't' => run == 1 ? "A/P" : "AM/PM",
            '/' => Escape(string.Concat(Enumerable.Repeat(patterns.DateSeparator, run))),
            ':' => Escape(string.Concat(Enumerable.Repeat(patterns.TimeSeparator, run))),
            _ => Escape(new string(specifier, run)),
        };
    }

    private static string Escape(string literal)
    {
        var escaped = new StringBuilder(literal.Length * 2);
        foreach (var character in literal)
        {
            if (!PlainLiterals.Contains(character, StringComparison.Ordinal))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }
}
