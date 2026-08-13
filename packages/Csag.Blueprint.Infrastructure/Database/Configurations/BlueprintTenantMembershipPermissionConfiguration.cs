namespace Csag.Blueprint.Infrastructure.Database.Configurations;

using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for <see cref="BlueprintTenantMembershipPermission{TUser,TTenant}"/>,
/// the tenant-scoped direct permission grant join entity (table <c>BlueprintTenantMembershipPermissions</c>).
/// </summary>
/// <typeparam name="TUser">The concrete application user type.</typeparam>
/// <typeparam name="TTenant">The concrete application tenant type.</typeparam>
public sealed class BlueprintTenantMembershipPermissionConfiguration<TUser, TTenant> : IEntityTypeConfiguration<BlueprintTenantMembershipPermission<TUser, TTenant>>
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BlueprintTenantMembershipPermission<TUser, TTenant>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BlueprintTenantMembershipPermissions");

        // The permission text is part of the key so a (user, tenant, permission) triple is unique and the
        // same permission can be granted independently to the same user in different tenants.
        builder.HasKey(e => new { e.UserId, e.TenantId, e.Permission });

        builder.Property(e => e.Permission).HasMaxLength(256).IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired(false);

        // FK back to the owning membership via the composite (UserId, TenantId).
        // Cascading from the membership cleans up direct permission grants when a user leaves a tenant.
        builder.HasOne(membershipPermission => membershipPermission.Membership)
            .WithMany(membership => membership.Permissions)
            .HasForeignKey(membershipPermission => new { membershipPermission.UserId, membershipPermission.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.TenantId);
    }
}
