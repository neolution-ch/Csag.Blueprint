namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for the blueprint-owned <see cref="BlueprintActiveSession"/> entity.
/// </summary>
public sealed class BlueprintActiveSessionConfiguration : IEntityTypeConfiguration<BlueprintActiveSession>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintActiveSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintActiveSessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.SessionKey).HasMaxLength(500).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(500);
        builder.Property(s => s.IpAddress).HasMaxLength(50);
        builder.Property(s => s.CurrentTenantId).IsRequired(false);

        // Indexes for efficient querying
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.SessionKey).IsUnique();
        builder.HasIndex(s => s.ExpiresAt);
    }
}
