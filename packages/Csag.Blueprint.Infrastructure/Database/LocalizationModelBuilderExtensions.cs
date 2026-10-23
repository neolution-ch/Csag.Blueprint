namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    /// <para>
    /// Everything is configured at convention level, so explicit configuration of the same aspect (a
    /// different delete behavior, for example) wins whether it runs before or after this call.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureLocalizedTextConventions(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Configures relationships for entities implementing <see cref="IHasLocalizedTexts{TLocalizedText}"/>.
    /// Sets up the one-to-many relationship between entities and their localized texts.
    /// <para>
    /// Unlike the model-wide conventions, this is explicit configuration of the types you name, like any
    /// fluent call: it overrides earlier configuration of the same aspects, and later configuration
    /// overrides it.
    /// </para>
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
    /// <para>
    /// Unlike the model-wide conventions, this is explicit configuration of the types you name, like any
    /// fluent call: it overrides earlier configuration of the same aspects, and later configuration
    /// overrides it.
    /// </para>
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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var index = modelBuilder.Entity<TLocalizedText>()
            .HasIndex(foreignKeyPropertyName, nameof(ILocalizedText.LanguageCode))
            .IsUnique();

        var name = EntityFilteringModelBuilderExtensions.TableScopedIndexName(
            index.Metadata.DeclaringEntityType, $"{foreignKeyPropertyName}_LanguageCode_Unique");
        if (name != null)
        {
            index.HasDatabaseName(name);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Comprehensive configuration for localized text relationships including constraints, indexes and relationships.
    /// This is a convenience method that applies standard configuration for localization.
    /// <para>
    /// The relationship and unique constraint are explicit configuration of the types you name (see
    /// <see cref="ConfigureLocalizedTextRelationship{TEntity, TLocalizedText}"/>); the text constraints
    /// are applied model-wide at convention level.
    /// </para>
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
    /// Applies <see cref="ConfigureLocalizedTextConventions(ModelBuilder)"/> through a convention-level builder.
    /// </summary>
    /// <param name="modelBuilder">The convention-level builder of the model to configure.</param>
    internal static void ConfigureLocalizedTextConventions(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
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

            var localizedEntityType = modelBuilder.Metadata.FindEntityType(localizedTextType);
            if (localizedEntityType == null)
            {
                continue;
            }

            var foreignKey = ResolveOwnerForeignKey(entityType, localizedEntityType);
            foreignKey.Builder.OnDeleteUnlessConfigured(DeleteBehavior.Cascade);

            var languageCode = localizedEntityType.FindProperty(nameof(ILocalizedText.LanguageCode));
            if (languageCode == null)
            {
                continue;
            }

            var foreignKeyPropertyNames = foreignKey.Properties.Select(p => p.Name);
            localizedEntityType.Builder
                .HasIndex([.. foreignKey.Properties, languageCode])
                ?.IsUnique(true)
                .HasTableScopedName(localizedEntityType, $"{string.Join('_', foreignKeyPropertyNames)}_LanguageCode_Unique");
        }
    }

    /// <summary>
    /// Resolves the relationship from the owner's <c>LocalizedTexts</c> navigation to its texts.
    /// </summary>
    /// <remarks>
    /// The contract only requires the <c>LocalizedTexts</c> navigation, never a foreign key named
    /// <c>&lt;Owner&gt;Id</c>, so the relationship EF has already discovered (or the consumer configured)
    /// is used whatever its foreign key is called. The conventional name is only the fallback for a
    /// model where no relationship exists yet. If neither resolves, this fails loudly rather than
    /// quietly leaving the entity without its cascade delete and per-language unique index.
    /// </remarks>
    /// <param name="ownerEntityType">The entity type implementing <see cref="IHasLocalizedTexts{T}"/>.</param>
    /// <param name="localizedEntityType">The localized text entity type.</param>
    /// <returns>The foreign key from the localized text entity to its owner.</returns>
    private static IConventionForeignKey ResolveOwnerForeignKey(IConventionEntityType ownerEntityType, IConventionEntityType localizedEntityType)
    {
        var navigation = ownerEntityType.FindNavigation(LocalizedTextsNavigationName);
        if (navigation != null && navigation.TargetEntityType == localizedEntityType)
        {
            return navigation.ForeignKey;
        }

        var ownerForeignKey = localizedEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == ownerEntityType);
        if (ownerForeignKey != null)
        {
            return ownerForeignKey;
        }

        var conventionalName = ownerEntityType.ClrType.Name + "Id";
        var conventionalProperty = localizedEntityType.FindProperty(conventionalName);
        var created = conventionalProperty == null
            ? null
            : localizedEntityType.Builder.HasRelationship(ownerEntityType, [conventionalProperty]);

        return created?.Metadata ?? throw new InvalidOperationException(
            $"Cannot configure localized texts for '{ownerEntityType.ClrType.Name}': no foreign key from " +
            $"'{localizedEntityType.ShortName()}' back to it was found, and there is no '{conventionalName}' " +
            $"property to fall back on. Configure the relationship explicitly in OnModelCreating.");
    }
}
