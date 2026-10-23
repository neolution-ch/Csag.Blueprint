namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Mimics the common application shape: entity types registered inside the derived
/// <c>OnModelCreating</c> after the base call, the way <c>ApplyConfigurationsFromAssembly</c> does.
/// </summary>
public sealed class LateRegistrationDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public LateRegistrationDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<EarlyNote> EarlyNotes => this.Set<EarlyNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<LateNote>();
    }
}
