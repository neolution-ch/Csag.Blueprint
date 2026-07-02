namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuration for the blueprint user inheritance root.
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
public sealed class BlueprintUserConfiguration<TUser> : IEntityTypeConfiguration<BlueprintUser>
    where TUser : BlueprintUser
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AspNetUsers");

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.PreferredLanguage).HasMaxLength(10);

        // TPH: base and derived user types share one table, distinguished by a discriminator column.
        builder.HasDiscriminator<string>("UserType")
            .HasValue<BlueprintUser>("BlueprintUser")
            .HasValue<TUser>(typeof(TUser).Name);
    }
}
