namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring string property constraints for domain contracts in Entity Framework Core models.
/// These extensions provide automatic configuration for entities implementing contracts with string properties.
/// </summary>
public static class ContractModelBuilderExtensions
{
    /// <summary>
    /// Configures default value SQL for single-column Guid primary keys.
    /// Applies <c>NEWSEQUENTIALID()</c> to keys that do not already define a default value SQL.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureGuidPrimaryKeyDefaults(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null || primaryKey.Properties.Count != 1)
            {
                continue;
            }

            var keyProperty = primaryKey.Properties[0];
            if (keyProperty.ClrType != typeof(Guid) || keyProperty.GetDefaultValueSql() != null)
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(keyProperty.Name)
                .HasDefaultValueSql("NEWSEQUENTIALID()");
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures string property constraints and indexes for entities implementing <see cref="IHasInternalName"/>.
    /// Sets the InternalName property to have a maximum length of 200 characters and creates an index for performance.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureInternalNameConstraints(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasInternalName).IsAssignableFrom(entityType.ClrType))
            {
                var tableName = entityType.GetTableName();

                // Configure InternalName property constraints
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IHasInternalName.InternalName))
                    .HasMaxLength(200)
                    .IsRequired();

                // Index on InternalName for efficient lookups
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(IHasInternalName.InternalName))
                    .HasDatabaseName($"IX_{tableName}_InternalName");
            }
        }

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
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ILocalizedText).IsAssignableFrom(entityType.ClrType))
            {
                var tableName = entityType.GetTableName();

                // Configure Text property constraints
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(ILocalizedText.Text))
                    .HasMaxLength(2000)
                    .IsRequired();

                // Configure LanguageCode property constraints
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(ILocalizedText.LanguageCode))
                    .HasMaxLength(10)
                    .IsRequired();

                // Index on LanguageCode for efficient language filtering
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(ILocalizedText.LanguageCode))
                    .HasDatabaseName($"IX_{tableName}_LanguageCode");
            }
        }

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
        return modelBuilder
            .ConfigureGuidPrimaryKeyDefaults()
            .ConfigureInternalNameConstraints()
            .ConfigureLocalizedTextConstraints();
    }
}
