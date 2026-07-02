namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint tenant inheritance root.
/// </summary>
/// <typeparam name="TTenant">The concrete application tenant type.</typeparam>
public sealed class BlueprintTenantConfiguration<TTenant> : IEntityTypeConfiguration<BlueprintTenant>
    where TTenant : BlueprintTenant
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTenants");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => e.Name);

        // TPH: base and derived tenant types share one table, distinguished by a discriminator column.
        builder.HasDiscriminator<string>("TenantType")
            .HasValue<BlueprintTenant>("BlueprintTenant")
            .HasValue<TTenant>(typeof(TTenant).Name);
    }
}
