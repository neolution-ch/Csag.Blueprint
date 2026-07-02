namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for the blueprint-owned <see cref="BlueprintAuditLog"/> entity.
/// </summary>
/// <remarks>
/// Production retention strategy: BlueprintAuditLog accumulates continuously and has no built-in expiry.
/// Before going to production, implement a retention mechanism appropriate for your compliance requirements.
/// Options include:
/// <list type="bullet">
///   <item>A scheduled background job that deletes rows older than N days</item>
///   <item>SQL Server table partitioning with partition switching for efficient bulk deletion</item>
///   <item>Archiving old rows to cold storage (e.g., Azure Blob, BigQuery) before deletion</item>
/// </list>
/// </remarks>
public sealed class BlueprintAuditLogConfiguration : IEntityTypeConfiguration<BlueprintAuditLog>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintAuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintAuditLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.JsonData).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(450);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);

        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.CreatedAt);
    }
}
