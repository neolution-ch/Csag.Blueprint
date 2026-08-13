namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="ITableViewMetadataLocalizer"/> backed by an <see cref="IStringLocalizer"/>.
/// Resolves the translation keys recorded on column metadata using the current request culture.
/// Produces localized copies of the affected columns — the definition-owned metadata instances are
/// never mutated, so the definition's DI lifetime cannot cause cross-request culture bleed.
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
        if (string.IsNullOrEmpty(column.DisplayNameKey) && string.IsNullOrEmpty(column.DescriptionKey))
        {
            return column;
        }

        var localized = column.Clone();
        localized.DisplayName = this.ResolveKey(column.DisplayNameKey, column.DisplayName, column.Name);
        localized.Description = this.ResolveKey(column.DescriptionKey, column.Description, column.Name);
        return localized;
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
