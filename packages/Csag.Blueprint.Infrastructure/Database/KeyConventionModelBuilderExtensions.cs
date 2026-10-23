namespace Csag.Blueprint.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;

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
    /// Keys that already declare a default, and keys declared <c>ValueGeneratedNever()</c>, are left
    /// untouched. A key that is also a foreign key, such as an owned type's, takes its value from the
    /// principal and is left alone too.
    /// </para>
    /// <para>
    /// EF Core keeps generating these keys client-side with its <see cref="SequentialGuidValueGenerator"/>,
    /// which is also ordered for SQL Server, so an added entity has its key before <c>SaveChanges</c>.
    /// The column default applies to inserts that bypass EF Core, such as raw SQL or seed scripts.
    /// </para>
    /// <para>
    /// <b>SQL Server only.</b> <c>NEWSEQUENTIALID()</c> does not exist on other providers, and they fail
    /// late rather than loudly: SQLite accepts the generated DDL and then rejects every insert with a
    /// <c>DbUpdateException</c>. <see cref="Conventions.BlueprintModelFinalizingConvention{TAppTenant, TAppUser, TAppRole}"/>
    /// applies this only when the context is running on SQL Server; call it directly only under the same
    /// condition.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureGuidPrimaryKeyDefaults(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureGuidPrimaryKeyDefaults(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Applies <see cref="ConfigureGuidPrimaryKeyDefaults(ModelBuilder)"/> through a convention-level builder.
    /// </summary>
    /// <param name="modelBuilder">The convention-level builder of the model to configure.</param>
    internal static void ConfigureGuidPrimaryKeyDefaults(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey == null || primaryKey.Properties.Count != 1)
            {
                continue;
            }

            var keyProperty = primaryKey.Properties[0];
            if (keyProperty.ClrType != typeof(Guid)
                || keyProperty.IsForeignKey()
                || keyProperty.ValueGenerated == ValueGenerated.Never)
            {
                continue;
            }

            // Skip keys that already declare a default, whether as SQL or as a CLR value. EF Core
            // rejects a property carrying both ("'DefaultValue' cannot be set ... at the same time as
            // 'DefaultValueSql'"), so adding one beside a consumer's HasDefaultValue(...) would make
            // the model fail to build.
            // TryGetDefaultValue is required here: GetDefaultValue() returns the CLR default
            // (Guid.Empty) for an unconfigured property, so it cannot distinguish "unset" from "set".
            if (keyProperty.GetDefaultValueSql() != null || keyProperty.TryGetDefaultValue(out _))
            {
                continue;
            }

            keyProperty.Builder.HasDefaultValueSql("NEWSEQUENTIALID()");

            // A database default alone makes EF Core treat the key as store-generated: an added entity
            // carries only a temporary key, and its CLR property reads Guid.Empty until SaveChanges, so
            // code copying it into a foreign key writes Guid.Empty. Keeping EF Core's client-side
            // generator assigns the key on Add as before; the column default serves inserts that bypass
            // EF Core.
            keyProperty.Builder.HasValueGenerator(typeof(SequentialGuidValueGenerator));
        }
    }
}
