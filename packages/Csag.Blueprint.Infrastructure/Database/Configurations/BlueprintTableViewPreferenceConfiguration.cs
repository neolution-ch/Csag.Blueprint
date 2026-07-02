namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for the blueprint-owned <see cref="BlueprintTableViewPreference{TUser}"/> entity.
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
public sealed class BlueprintTableViewPreferenceConfiguration<TUser> : IEntityTypeConfiguration<BlueprintTableViewPreference<TUser>>
    where TUser : BlueprintUser
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTableViewPreference<TUser>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTableViewPreferences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.TableViewId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired();
        builder.Property(e => e.PreferencesJson).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired(false);

        // Create a composite unique index on UserId + TableViewId + Name
        // Each user can have multiple named preferences per table view, but names must be unique
        builder.HasIndex(e => new { e.UserId, e.TableViewId, e.Name })
            .IsUnique();

        // Create an index on TableViewId for efficient lookups.
        builder.HasIndex(e => e.TableViewId);

        // Foreign key relationship to the concrete application user.
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
