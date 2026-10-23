namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Extension methods for configuring string property constraints for domain contracts in Entity Framework Core models.
/// These extensions provide automatic configuration for entities implementing contracts with string properties.
/// <para>
/// Everything is configured at convention level, so explicit configuration of the same aspect (a
/// different maximum length, for example) wins whether it runs before or after these methods.
/// </para>
/// </summary>
public static class ContractModelBuilderExtensions
{
    /// <summary>
    /// Configures string property constraints and indexes for entities implementing <see cref="IHasInternalName"/>.
    /// Sets the InternalName property to have a maximum length of 200 characters and creates an index for performance.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureInternalNameConstraints(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureInternalNameConstraints(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Configures string property constraints and indexes for entities implementing <see cref="ILocalizedText"/>.
    /// Sets the Text property to have a maximum length of 2000 characters and the LanguageCode property
    /// to have a maximum length of 10 characters, with appropriate indexes.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureLocalizedTextConstraints(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureLocalizedTextConstraints(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Configures string property constraints for entities implementing domain contracts.
    /// This is a convenience method that applies all contract-based configurations.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureContractConstraints(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureContractConstraints(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Applies <see cref="ConfigureContractConstraints(ModelBuilder)"/> through a convention-level builder.
    /// </summary>
    /// <param name="modelBuilder">The convention-level builder of the model to configure.</param>
    internal static void ConfigureContractConstraints(IConventionModelBuilder modelBuilder)
    {
        ConfigureInternalNameConstraints(modelBuilder);
        ConfigureLocalizedTextConstraints(modelBuilder);
    }

    private static void ConfigureInternalNameConstraints(IConventionModelBuilder modelBuilder)
    {
        // Only the type that declares the contract: in a TPH hierarchy every mapped type reports the
        // same table, so configuring each of them would redefine one index repeatedly.
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(EntityFilteringModelBuilderExtensions.DeclaresContract<IHasInternalName>))
        {
            var internalName = ConfigureRequiredString(entityType, nameof(IHasInternalName.InternalName), maxLength: 200);
            if (internalName != null)
            {
                entityType.Builder
                    .HasIndex([internalName])
                    .HasTableScopedName(entityType, "InternalName");
            }
        }
    }

    private static void ConfigureLocalizedTextConstraints(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(EntityFilteringModelBuilderExtensions.DeclaresContract<ILocalizedText>))
        {
            ConfigureRequiredString(entityType, nameof(ILocalizedText.Text), maxLength: 2000);

            var languageCode = ConfigureRequiredString(entityType, nameof(ILocalizedText.LanguageCode), maxLength: 10);
            if (languageCode != null)
            {
                entityType.Builder
                    .HasIndex([languageCode])
                    .HasTableScopedName(entityType, "LanguageCode");
            }
        }
    }

    private static IConventionProperty? ConfigureRequiredString(IConventionEntityType entityType, string propertyName, int maxLength)
    {
        // A property the application has ignored is left ignored.
        var property = entityType.FindProperty(propertyName);
        if (property == null)
        {
            return null;
        }

        property.Builder.HasMaxLength(maxLength);
        property.Builder.IsRequired(true);
        return property;
    }
}
