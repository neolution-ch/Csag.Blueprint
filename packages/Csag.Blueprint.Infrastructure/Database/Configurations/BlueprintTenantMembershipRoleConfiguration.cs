namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for <see cref="BlueprintTenantMembershipRole{TUser,TTenant}"/>,
/// the tenant-scoped role assignment join entity (table <c>BlueprintTenantMembershipRoles</c>).
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TTenant">The concrete application tenant type.</typeparam>
public sealed class BlueprintTenantMembershipRoleConfiguration<TUser, TTenant> : IEntityTypeConfiguration<BlueprintTenantMembershipRole<TUser, TTenant>>
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTenantMembershipRole<TUser, TTenant>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTenantMembershipRoles");

        // Composite primary key mirrors AspNetUserRoles' (UserId, RoleId) but adds TenantId so the
        // same (user, role) pair can exist independently in multiple tenants.
        builder.HasKey(e => new { e.UserId, e.TenantId, e.RoleId });

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired(false);

        // FK back to the owning membership via the composite (UserId, TenantId).
        // Cascading from the membership cleans up role assignments when a user leaves a tenant.
        builder.HasOne(membershipRole => membershipRole.Membership)
            .WithMany(membership => membership.Roles)
            .HasForeignKey(membershipRole => new { membershipRole.UserId, membershipRole.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the shared role catalog (AspNetRoles / BlueprintRole). Restrict avoids a multiple-
        // cascade-path conflict on SQL Server (the membership already cascades from the user), while
        // still keeping referential integrity. Role catalog rows are effectively immutable in practice.
        builder.HasOne<BlueprintRole>()
            .WithMany()
            .HasForeignKey(membershipRole => membershipRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.RoleId);
    }
}
