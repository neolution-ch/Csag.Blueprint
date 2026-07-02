namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// Defines a contract for the table view preferences service.
/// Manages the lifecycle (List/Get/Save/Update/Delete) of named user preferences for table views.
/// </summary>
public interface ITableViewPreferencesService
{
    /// <summary>
    /// Lists all saved preference summaries for a user and table view.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tableViewId">The table view identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of preference summaries, ordered by creation date.</returns>
    Task<IList<TableViewPreferenceSummary>> GetAllPreferencesAsync(
        Guid userId,
        string tableViewId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific saved preference by its unique identifier.
    /// </summary>
    /// <param name="userId">The user identifier (for ownership verification).</param>
    /// <param name="preferenceId">The preference identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preferences model if found and owned by the user; otherwise, null.</returns>
    Task<TableViewPreferencesModel?> GetPreferenceByIdAsync(
        Guid userId,
        Guid preferenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's default preference for a specific table view.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tableViewId">The table view identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default preferences model if found; otherwise, null.</returns>
    Task<TableViewPreferencesModel?> GetDefaultPreferenceAsync(
        Guid userId,
        string tableViewId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new named preference for a table view.
    /// If <see cref="TableViewPreferencesModel.IsDefault"/> is true, any existing default for the same view is unset.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tableViewId">The table view identifier.</param>
    /// <param name="preferences">The preferences model to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unique identifier of the created preference.</returns>
    Task<Guid> CreatePreferenceAsync(
        Guid userId,
        string tableViewId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing preference.
    /// If <see cref="TableViewPreferencesModel.IsDefault"/> is true, any existing default for the same view is unset.
    /// </summary>
    /// <param name="userId">The user identifier (for ownership verification).</param>
    /// <param name="preferenceId">The preference identifier to update.</param>
    /// <param name="preferences">The updated preferences model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated; false if not found or not owned by user.</returns>
    Task<bool> UpdatePreferenceAsync(
        Guid userId,
        Guid preferenceId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a specific preference as the default for its table view, unsetting any previous default.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tableViewId">The table view identifier.</param>
    /// <param name="preferenceId">The preference identifier to set as default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the default was set; false if the preference was not found.</returns>
    Task<bool> SetDefaultAsync(
        Guid userId,
        string tableViewId,
        Guid preferenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific preference by its unique identifier.
    /// </summary>
    /// <param name="userId">The user identifier (for ownership verification).</param>
    /// <param name="preferenceId">The preference identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted; false if not found or not owned by user.</returns>
    Task<bool> DeletePreferenceAsync(
        Guid userId,
        Guid preferenceId,
        CancellationToken cancellationToken = default);
}
