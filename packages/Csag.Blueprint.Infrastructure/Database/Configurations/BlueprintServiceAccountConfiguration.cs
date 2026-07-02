namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint-owned <see cref="BlueprintServiceAccount"/> entity.
/// </summary>
public sealed class BlueprintServiceAccountConfiguration : IEntityTypeConfiguration<BlueprintServiceAccount>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintServiceAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintServiceAccounts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ClientId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ClientSecretHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Roles).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Permissions).HasColumnType("nvarchar(max)");
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // Unique index on ClientId
        builder.HasIndex(e => e.ClientId).IsUnique();
    }
}
