namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint-owned <see cref="BlueprintResourceAccess"/> entity.
/// </summary>
public sealed class BlueprintResourceAccessConfiguration : IEntityTypeConfiguration<BlueprintResourceAccess>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintResourceAccess> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintResourceAccess");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EntityId).IsRequired();
        builder.Property(e => e.ResourceType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ResourceId).IsRequired(false);
        builder.Property(e => e.Permission).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // Indexes for query performance
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => new { e.ResourceType, e.ResourceId });
    }
}
