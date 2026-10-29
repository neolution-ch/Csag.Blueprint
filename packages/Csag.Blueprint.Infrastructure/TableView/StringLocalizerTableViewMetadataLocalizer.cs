namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ITableViewMetadataLocalizer"/> backed by an <see cref="IStringLocalizer"/>.
/// Resolves the translation keys recorded on column metadata using the current request culture.
/// Produces localized copies of the affected columns — the definition-owned metadata instances are
/// never mutated, so the definition's DI lifetime cannot cause cross-request culture bleed.
/// A key that cannot be resolved keeps the definition's text and is logged as a warning, except
/// for allowed-value keys marked <see cref="TableViewAllowedValue.IsLabelKeyOptional"/> (the keys
/// enum columns derive by convention), which fall back silently.
/// </summary>
public sealed class StringLocalizerTableViewMetadataLocalizer : ITableViewMetadataLocalizer
{
    private readonly IStringLocalizer localizer;
    private readonly ILogger<StringLocalizerTableViewMetadataLocalizer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringLocalizerTableViewMetadataLocalizer"/> class.
    /// </summary>
    /// <param name="localizer">The string localizer used to resolve translation keys.</param>
    /// <param name="logger">The logger.</param>
    public StringLocalizerTableViewMetadataLocalizer(
        IStringLocalizer localizer,
        ILogger<StringLocalizerTableViewMetadataLocalizer> logger)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(logger);

        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public IList<TableViewColumnMetadata> Localize(IList<TableViewColumnMetadata> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var result = new List<TableViewColumnMetadata>(columns.Count);
        foreach (var column in columns)
        {
            result.Add(this.LocalizeColumn(column));
        }

        return result;
    }

    private TableViewColumnMetadata LocalizeColumn(TableViewColumnMetadata column)
    {
        var localizedAllowedValues = this.LocalizeAllowedValues(column.AllowedValues, column.Name);
        if (string.IsNullOrEmpty(column.DisplayNameKey)
            && string.IsNullOrEmpty(column.DescriptionKey)
            && localizedAllowedValues is null)
        {
            return column;
        }

        var localized = column.Clone();
        localized.DisplayName = this.ResolveKey(column.DisplayNameKey, column.DisplayName, column.Name);
        localized.Description = this.ResolveKey(column.DescriptionKey, column.Description, column.Name);
        if (localizedAllowedValues is not null)
        {
            localized.AllowedValues = localizedAllowedValues;
        }

        return localized;
    }

    /// <summary>
    /// Returns a new list with the label of every keyed allowed value resolved, or <c>null</c> when
    /// no allowed value carries a key.
    /// </summary>
    private TableViewAllowedValue[]? LocalizeAllowedValues(IReadOnlyList<TableViewAllowedValue>? allowedValues, string columnName)
    {
        if (allowedValues is null || !allowedValues.Any(value => !string.IsNullOrEmpty(value.LabelKey)))
        {
            return null;
        }

        var localized = new TableViewAllowedValue[allowedValues.Count];
        for (var i = 0; i < localized.Length; i++)
        {
            var value = allowedValues[i];
            localized[i] = string.IsNullOrEmpty(value.LabelKey)
                ? value
                : value with { Label = this.ResolveLabelKey(value.LabelKey, value, columnName) };
        }

        return localized;
    }

    private string ResolveLabelKey(string labelKey, TableViewAllowedValue value, string columnName)
    {
        var translation = this.localizer[labelKey];
        if (!translation.ResourceNotFound)
        {
            return translation.Value;
        }

        if (!value.IsLabelKeyOptional)
        {
            this.logger.LogWarning(
                "Translation key '{TranslationKey}' for allowed value '{AllowedValue}' of table view column '{ColumnName}' was not found; serving the default label instead.",
                labelKey,
                value.Value,
                columnName);
        }

        return value.Label;
    }

    private string ResolveKey(string? translationKey, string fallback, string columnName)
    {
        if (string.IsNullOrEmpty(translationKey))
        {
            return fallback;
        }

        var value = this.localizer[translationKey];
        if (value.ResourceNotFound)
        {
            this.logger.LogWarning(
                "Translation key '{TranslationKey}' for table view column '{ColumnName}' was not found; serving the default text instead.",
                translationKey,
                columnName);
            return fallback;
        }

        return value.Value;
    }
}
