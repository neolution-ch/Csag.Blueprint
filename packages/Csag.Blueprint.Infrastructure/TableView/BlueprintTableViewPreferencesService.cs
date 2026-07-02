namespace Csag.Blueprint.Infrastructure.TableView;

using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service implementation for managing table view preferences.
/// Supports multiple named views per user per table, with a default flag.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
/// <typeparam name="TUser">The user entity type.</typeparam>
public sealed class BlueprintTableViewPreferencesService<TContext, TUser> : ITableViewPreferencesService
    where TContext : DbContext
    where TUser : BlueprintUser
{
    private readonly TContext context;
    private readonly ILogger<BlueprintTableViewPreferencesService<TContext, TUser>> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintTableViewPreferencesService{TContext, TUser}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger.</param>
    public BlueprintTableViewPreferencesService(
        TContext context,
        ILogger<BlueprintTableViewPreferencesService<TContext, TUser>> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IList<TableViewPreferenceSummary>> GetAllPreferencesAsync(
        Guid userId,
        string tableViewId,
        CancellationToken cancellationToken = default)
    {
        return await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.TableViewId == tableViewId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new TableViewPreferenceSummary
            {
                Id = p.Id,
                Name = p.Name,
                IsDefault = p.IsDefault,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TableViewPreferencesModel?> GetPreferenceByIdAsync(
        Guid userId,
        Guid preferenceId,
        CancellationToken cancellationToken = default)
    {
        var preference = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == preferenceId && p.UserId == userId,
                cancellationToken);

        return this.DeserializePreference(preference);
    }

    /// <inheritdoc/>
    public async Task<TableViewPreferencesModel?> GetDefaultPreferenceAsync(
        Guid userId,
        string tableViewId,
        CancellationToken cancellationToken = default)
    {
        var preference = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.UserId == userId && p.TableViewId == tableViewId && p.IsDefault,
                cancellationToken);

        return this.DeserializePreference(preference);
    }

    /// <inheritdoc/>
    public Task<Guid> CreatePreferenceAsync(
        Guid userId,
        string tableViewId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return this.CreatePreferenceInternalAsync(userId, tableViewId, preferences, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> UpdatePreferenceAsync(
        Guid userId,
        Guid preferenceId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return this.UpdatePreferenceInternalAsync(userId, preferenceId, preferences, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SetDefaultAsync(
        Guid userId,
        string tableViewId,
        Guid preferenceId,
        CancellationToken cancellationToken = default)
    {
        var preference = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .FirstOrDefaultAsync(
                p => p.Id == preferenceId && p.UserId == userId && p.TableViewId == tableViewId,
                cancellationToken);

        if (preference == null)
        {
            return false;
        }

        await this.UnsetDefaultsAsync(userId, tableViewId, cancellationToken);

        preference.IsDefault = true;
        preference.UpdatedAt = DateTimeOffset.UtcNow;

        await this.context.SaveChangesAsync(cancellationToken);

        this.logger.LogDebug(
            "Set preference '{PreferenceId}' as default for user '{UserId}' and table view '{TableViewId}'",
            preferenceId,
            userId,
            tableViewId);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeletePreferenceAsync(
        Guid userId,
        Guid preferenceId,
        CancellationToken cancellationToken = default)
    {
        var preference = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .FirstOrDefaultAsync(
                p => p.Id == preferenceId && p.UserId == userId,
                cancellationToken);

        if (preference == null)
        {
            return false;
        }

        this.context.Set<BlueprintTableViewPreference<TUser>>().Remove(preference);
        await this.context.SaveChangesAsync(cancellationToken);

        this.logger.LogDebug(
            "Deleted preference '{PreferenceId}' for user '{UserId}'",
            preferenceId,
            userId);

        return true;
    }

    private async Task<Guid> CreatePreferenceInternalAsync(
        Guid userId,
        string tableViewId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken)
    {
        preferences.TableViewId = tableViewId;

        if (preferences.IsDefault)
        {
            await this.UnsetDefaultsAsync(userId, tableViewId, cancellationToken);
        }

        var json = JsonSerializer.Serialize(preferences, BlueprintJsonOptions.Default);
        var now = DateTimeOffset.UtcNow;

        var newPreference = new BlueprintTableViewPreference<TUser>
        {
            UserId = userId,
            TableViewId = tableViewId,
            Name = preferences.Name,
            IsDefault = preferences.IsDefault,
            PreferencesJson = json,
            CreatedAt = now,
        };

        await this.context.Set<BlueprintTableViewPreference<TUser>>().AddAsync(newPreference, cancellationToken);
        await this.context.SaveChangesAsync(cancellationToken);

        this.logger.LogDebug(
            "Created preference '{Name}' for user '{UserId}' and table view '{TableViewId}'",
            preferences.Name,
            userId,
            tableViewId);

        return newPreference.Id;
    }

    private async Task<bool> UpdatePreferenceInternalAsync(
        Guid userId,
        Guid preferenceId,
        TableViewPreferencesModel preferences,
        CancellationToken cancellationToken)
    {
        var existing = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .FirstOrDefaultAsync(
                p => p.Id == preferenceId && p.UserId == userId,
                cancellationToken);

        if (existing == null)
        {
            return false;
        }

        preferences.TableViewId = existing.TableViewId;

        if (preferences.IsDefault)
        {
            await this.UnsetDefaultsAsync(userId, existing.TableViewId, cancellationToken);
        }

        var json = JsonSerializer.Serialize(preferences, BlueprintJsonOptions.Default);

        existing.Name = preferences.Name;
        existing.IsDefault = preferences.IsDefault;
        existing.PreferencesJson = json;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await this.context.SaveChangesAsync(cancellationToken);

        this.logger.LogDebug(
            "Updated preference '{PreferenceId}' for user '{UserId}'",
            preferenceId,
            userId);

        return true;
    }

    private async Task UnsetDefaultsAsync(
        Guid userId,
        string tableViewId,
        CancellationToken cancellationToken)
    {
        var currentDefaults = await this.context.Set<BlueprintTableViewPreference<TUser>>()
            .Where(p => p.UserId == userId && p.TableViewId == tableViewId && p.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var item in currentDefaults)
        {
            item.IsDefault = false;
        }
    }

    private TableViewPreferencesModel? DeserializePreference(BlueprintTableViewPreference<TUser>? preference)
    {
        if (preference == null)
        {
            return null;
        }

        try
        {
            var model = JsonSerializer.Deserialize<TableViewPreferencesModel>(
                preference.PreferencesJson,
                BlueprintJsonOptions.Default);

            if (model != null)
            {
                model.TableViewId = preference.TableViewId;
                model.Name = preference.Name;
                model.IsDefault = preference.IsDefault;
            }

            return model;
        }
        catch (JsonException ex)
        {
            this.logger.LogError(
                ex,
                "Failed to deserialize preferences for preference '{PreferenceId}'",
                preference.Id);
            return null;
        }
    }
}
