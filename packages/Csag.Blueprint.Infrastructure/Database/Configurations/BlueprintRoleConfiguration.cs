namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint role inheritance root.
/// </summary>
/// <typeparam name="TRole">The concrete application role type.</typeparam>
public sealed class BlueprintRoleConfiguration<TRole> : IEntityTypeConfiguration<BlueprintRole>
    where TRole : BlueprintRole
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AspNetRoles");

        builder.Property(e => e.CreatedAt).IsRequired();

        // TPH: base and derived role types share one table, distinguished by a discriminator column.
        builder.HasDiscriminator<string>("RoleType")
            .HasValue<BlueprintRole>("BlueprintRole")
            .HasValue<TRole>(typeof(TRole).Name);
    }
}
