namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for the blueprint-owned <see cref="BlueprintServiceAccountSession"/> entity.
/// </summary>
public sealed class BlueprintServiceAccountSessionConfiguration : IEntityTypeConfiguration<BlueprintServiceAccountSession>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintServiceAccountSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintServiceAccountSessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(s => s.ServiceAccountId).IsRequired();
        builder.Property(s => s.SessionKey).HasMaxLength(500).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(500);
        builder.Property(s => s.IpAddress).HasMaxLength(50);

        // Indexes for lookup-by-key (validation), revocation and cleanup by account, and the table-wide expiry
        // sweep in IServiceAccountSessionManager.CleanupExpiredSessionsAsync.
        builder.HasIndex(s => s.ServiceAccountId);
        builder.HasIndex(s => s.SessionKey).IsUnique();
        builder.HasIndex(s => s.ExpiresAt);
    }
}
