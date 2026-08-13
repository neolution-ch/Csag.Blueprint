namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Applies request-time localization to table view column metadata.
/// Column definitions record translation keys at construction time (see
/// <see cref="TableViewColumnDefinition{TEntity, TDto}.WithTranslatedDisplayName"/> and
/// <see cref="TableViewColumnDefinition{TEntity, TDto}.WithTranslatedDescription"/>) without touching
/// any localizer or the database; implementations of this interface resolve those keys when a request
/// is handled, using the request's current culture.
/// </summary>
/// <remarks>
/// A no-op implementation is registered by the table view registration so consumers can use table
/// views without any localization package; database-backed localization replaces it when enabled.
/// Implementations must never mutate the given metadata instances — they are owned by the table view
/// definition. A column whose text changes must be returned as a copy; unchanged columns may be
/// returned as-is. Callers must treat the returned list and its items as read-only.
/// </remarks>
public interface ITableViewMetadataLocalizer
{
    /// <summary>
    /// Resolves the localized display name and description for every column that carries a
    /// translation key. Columns without keys are returned unchanged.
    /// </summary>
    /// <param name="columns">The column metadata to localize. The instances are not mutated.</param>
    /// <returns>The localized column metadata, in the same order as <paramref name="columns"/>.</returns>
    IList<TableViewColumnMetadata> Localize(IList<TableViewColumnMetadata> columns);
}
