namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Default <see cref="ITableViewMetadataLocalizer"/> that performs no localization and returns the
/// column metadata unchanged. Registered by the table view registration so table views work without
/// any localization package; database-backed localization replaces this registration when enabled.
/// </summary>
public sealed class NoOpTableViewMetadataLocalizer : ITableViewMetadataLocalizer
{
    /// <inheritdoc/>
    public IList<TableViewColumnMetadata> Localize(IList<TableViewColumnMetadata> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        return columns;
    }
}
