namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint-owned <see cref="BlueprintTranslation"/> entity.
/// </summary>
public sealed class BlueprintTranslationConfiguration : IEntityTypeConfiguration<BlueprintTranslation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTranslation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTranslations");

        builder.HasKey(e => e.TranslationId);
        builder.Property(e => e.TranslationId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Key).HasMaxLength(256).IsRequired();
        builder.Property(e => e.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(4000);
        builder.HasIndex(e => new { e.Key, e.LanguageCode }).IsUnique();
        builder.HasIndex(e => e.LanguageCode);
    }
}
