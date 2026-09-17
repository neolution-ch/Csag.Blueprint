namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

            // Type.GetInterfaces() reports interfaces inherited from base classes too, so every derived
            // type in a hierarchy re-reports its base's contract. Configuring it again from the derived
            // type looks for a foreign key whose principal is the derived CLR type, which does not
            // exist — the relationship belongs to the base. Only the type that introduces the contract
            // is configured; a derived type adding a *different* localized-text contract still is.
            if (entityType.BaseType != null && localizedContract.IsAssignableFrom(entityType.BaseType.ClrType))
            {
                continue;
            }

            var localizedTextType = localizedContract.GetGenericArguments()[0];
            if (!typeof(ILocalizedText).IsAssignableFrom(localizedTextType))
            {
                continue;
            }

            var localizedEntityType = modelBuilder.Model.FindEntityType(localizedTextType);
            if (localizedEntityType == null)
            {
                continue;
            }

            var foreignKeyPropertyNames = ResolveOwnerForeignKey(localizedEntityType, entityType.ClrType);

            var inverseNavigation = localizedEntityType.GetNavigations()
                .FirstOrDefault(n => n.TargetEntityType.ClrType == entityType.ClrType)?.Name;

            var relationshipBuilder = modelBuilder.Entity(entityType.ClrType)
                .HasMany(localizedTextType, LocalizedTextsNavigationName);

            if (inverseNavigation != null)
            {
                relationshipBuilder
                    .WithOne(inverseNavigation)
                    .HasForeignKey(foreignKeyPropertyNames)
                    .OnDelete(DeleteBehavior.Cascade);
            }
            else
            {
                relationshipBuilder
                    .WithOne()
                    .HasForeignKey(foreignKeyPropertyNames)
                    .OnDelete(DeleteBehavior.Cascade);
            }

            var localizedTableName = localizedEntityType.GetTableName();
            var indexProperties = new List<string>(foreignKeyPropertyNames) { nameof(ILocalizedText.LanguageCode) };
            modelBuilder.Entity(localizedTextType)
                .HasIndex([.. indexProperties])
                .IsUnique()
                .HasDatabaseName($"IX_{localizedTableName}_{string.Join('_', foreignKeyPropertyNames)}_LanguageCode_Unique");
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
            .ConfigureLocalizedTextRelationship<TEntity, TLocalizedText>(foreignKeyPropertyName)
            .ConfigureLocalizedTextUniqueConstraint<TLocalizedText>(foreignKeyPropertyName);
    }

    /// <summary>
    /// Resolves the foreign key on the localized text entity that points back at its owner.
    /// </summary>
    /// <remarks>
    /// The contract only requires the <c>LocalizedTexts</c> navigation, never a foreign key named
    /// <c>&lt;Owner&gt;Id</c>. Assuming that name meant a consumer who called theirs anything else was
    /// silently skipped — no cascade delete and, more importantly, no per-language unique index —
    /// even though this method claims to configure every <see cref="IHasLocalizedTexts{T}"/> entity.
    /// So the relationship EF has already discovered (or the consumer configured) wins, and the
    /// conventional name is only the fallback for a model where none exists yet. If neither resolves,
    /// we fail loudly rather than quietly leaving the entity unconfigured.
    /// </remarks>
    /// <param name="localizedEntityType">The localized text entity type.</param>
    /// <param name="ownerClrType">The CLR type of the owning entity.</param>
    /// <returns>The foreign key property names, in key order.</returns>
    private static string[] ResolveOwnerForeignKey(IReadOnlyEntityType localizedEntityType, Type ownerClrType)
    {
        var ownerForeignKey = localizedEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == ownerClrType);

        if (ownerForeignKey != null)
        {
            return [.. ownerForeignKey.Properties.Select(p => p.Name)];
        }

        var conventionalName = ownerClrType.Name + "Id";
        if (localizedEntityType.FindProperty(conventionalName) != null)
        {
            return [conventionalName];
        }

        throw new InvalidOperationException(
            $"Cannot configure localized texts for '{ownerClrType.Name}': no foreign key from " +
            $"'{localizedEntityType.ShortName()}' back to it was found, and there is no '{conventionalName}' " +
            $"property to fall back on. Configure the relationship explicitly in OnModelCreating before " +
            $"calling {nameof(ConfigureLocalizedTextConventions)}().");
    }
}
