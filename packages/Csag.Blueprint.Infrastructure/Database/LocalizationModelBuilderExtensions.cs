namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring localized text relationships in Entity Framework Core models.
/// These extensions provide automatic configuration for entities implementing localization contracts.
/// </summary>
public static class LocalizationModelBuilderExtensions
{
    private const string LocalizedTextsNavigationName = "LocalizedTexts";

    /// <summary>
    /// Automatically configures localized text relationships and unique constraints for all
    /// entities implementing <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var localizedContract = entityType.ClrType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasLocalizedTexts<>));

            if (localizedContract == null)
            {
                continue;
            }

            var localizedTextType = localizedContract.GetGenericArguments()[0];
            if (!typeof(ILocalizedText).IsAssignableFrom(localizedTextType))
            {
                continue;
            }

            var foreignKeyPropertyName = entityType.ClrType.Name + "Id";
            var localizedEntityType = modelBuilder.Model.FindEntityType(localizedTextType);
            if (localizedEntityType == null || localizedEntityType.FindProperty(foreignKeyPropertyName) == null)
            {
                continue;
            }

            var inverseNavigation = localizedEntityType.GetNavigations()
                .FirstOrDefault(n => n.TargetEntityType.ClrType == entityType.ClrType)?.Name;

            var relationshipBuilder = modelBuilder.Entity(entityType.ClrType)
                .HasMany(localizedTextType, LocalizedTextsNavigationName);

            if (inverseNavigation != null)
            {
                relationshipBuilder
                    .WithOne(inverseNavigation)
                    .HasForeignKey(foreignKeyPropertyName)
                    .OnDelete(DeleteBehavior.Cascade);
            }
            else
            {
                relationshipBuilder
                    .WithOne()
                    .HasForeignKey(foreignKeyPropertyName)
                    .OnDelete(DeleteBehavior.Cascade);
            }

            var localizedTableName = localizedEntityType.GetTableName();
            modelBuilder.Entity(localizedTextType)
                .HasIndex(foreignKeyPropertyName, nameof(ILocalizedText.LanguageCode))
                .IsUnique()
                .HasDatabaseName($"IX_{localizedTableName}_{foreignKeyPropertyName}_LanguageCode_Unique");
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures indexes for entities implementing <see cref="ILocalizedText"/>.
    /// Creates indexes on LanguageCode for better query performance.
    /// Note: String property constraints are configured by <see cref="ContractModelBuilderExtensions.ConfigureLocalizedTextConstraints"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextIndexes(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ILocalizedText).IsAssignableFrom(entityType.ClrType))
            {
                var tableName = entityType.GetTableName();

                // Index on LanguageCode for efficient language filtering
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(ILocalizedText.LanguageCode))
                    .HasDatabaseName($"IX_{tableName}_LanguageCode");
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures relationships for entities implementing <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// Sets up the one-to-many relationship between entities and their localized texts.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="foreignKeyPropertyName">
    /// The name of the foreign key property on the localized text entity.
    /// If not specified, EF Core conventions will be used.
    /// </param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextRelationship<TEntity, TLocalizedText>(
        this ModelBuilder modelBuilder,
        string? foreignKeyPropertyName = null)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        var entityBuilder = modelBuilder.Entity<TEntity>();

        if (string.IsNullOrEmpty(foreignKeyPropertyName))
        {
            // Use EF Core conventions for foreign key naming
            entityBuilder
                .HasMany(e => e.LocalizedTexts)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
        }
        else
        {
            // Use explicit foreign key property name
            entityBuilder
                .HasMany(e => e.LocalizedTexts)
                .WithOne()
                .HasForeignKey(foreignKeyPropertyName)
                .OnDelete(DeleteBehavior.Cascade);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures a composite unique constraint on the foreign key and language code
    /// to ensure only one text entry per language per entity.
    /// </summary>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="foreignKeyPropertyName">The name of the foreign key property on the localized text entity.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextUniqueConstraint<TLocalizedText>(
        this ModelBuilder modelBuilder,
        string foreignKeyPropertyName)
        where TLocalizedText : class, ILocalizedText
    {
        var tableName = modelBuilder.Entity<TLocalizedText>().Metadata.GetTableName();

        modelBuilder.Entity<TLocalizedText>()
            .HasIndex(foreignKeyPropertyName, nameof(ILocalizedText.LanguageCode))
            .IsUnique()
            .HasDatabaseName($"IX_{tableName}_{foreignKeyPropertyName}_LanguageCode_Unique");

        return modelBuilder;
    }

    /// <summary>
    /// Comprehensive configuration for localized text relationships including constraints, indexes and relationships.
    /// This is a convenience method that applies standard configuration for localization.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that has localized texts.</typeparam>
    /// <typeparam name="TLocalizedText">The localized text entity type.</typeparam>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="foreignKeyPropertyName">The name of the foreign key property on the localized text entity.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalization<TEntity, TLocalizedText>(
        this ModelBuilder modelBuilder,
        string foreignKeyPropertyName)
        where TEntity : class, IHasLocalizedTexts<TLocalizedText>
        where TLocalizedText : class, ILocalizedText
    {
        return modelBuilder
            .ConfigureLocalizedTextConstraints()
            .ConfigureLocalizedTextIndexes()
            .ConfigureLocalizedTextRelationship<TEntity, TLocalizedText>(foreignKeyPropertyName)
            .ConfigureLocalizedTextUniqueConstraint<TLocalizedText>(foreignKeyPropertyName);
    }
}
