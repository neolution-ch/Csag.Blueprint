namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Application-shaped context with tenant-owned entity types registered after the base call, with no
/// <c>DbSet</c>.
/// </summary>
public sealed class TenantScopedDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public TenantScopedDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Booking>();
        builder.Entity<Memo>();
    }
}
