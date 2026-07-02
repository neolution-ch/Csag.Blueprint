namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for the blueprint-owned <see cref="BlueprintTenantMembership{TUser,TTenant}"/> entity.
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TTenant">The concrete application tenant type.</typeparam>
public sealed class BlueprintTenantMembershipConfiguration<TUser, TTenant> : IEntityTypeConfiguration<BlueprintTenantMembership<TUser, TTenant>>
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTenantMembership<TUser, TTenant>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTenantMemberships");

        builder.HasKey(e => new { e.UserId, e.TenantId });

        builder.Property(e => e.JoinedAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired(false);

        builder.HasOne(membership => membership.User)
            .WithMany("TenantMemberships")
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(membership => membership.Tenant)
            .WithMany("Memberships")
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.UserId);
    }
}
