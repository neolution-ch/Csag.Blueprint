namespace Csag.Blueprint.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for primary key conventions applied across the whole model.
/// <para>
/// Unlike the contract-driven conventions, these apply to <b>every</b> entity in the model, including
/// consumer entities that implement none of the <c>Csag.Blueprint.Domain</c> contracts. Adopting them
/// therefore produces a migration in the consuming application.
/// </para>
/// </summary>
public static class KeyConventionModelBuilderExtensions
{
    /// <summary>
    /// Applies <c>NEWSEQUENTIALID()</c> as the default value for single-column <see cref="Guid"/>
    /// primary keys that do not already declare one, so inserts stay sequential and clustered index
    /// fragmentation stays low.
    /// <para>
    /// Keys configured explicitly elsewhere are left untouched, and keys assigned in application code
    /// keep working — the default only applies when no value is provided. This is SQL Server specific.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureGuidPrimaryKeyDefaults(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

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
}
