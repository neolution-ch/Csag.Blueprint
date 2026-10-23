namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Application-shaped context that configures an entity type explicitly after the base call,
/// overriding settings the blueprint conventions would otherwise choose.
/// </summary>
public sealed class ExplicitConfigurationDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public ExplicitConfigurationDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Gets the index name the application chooses for <see cref="Ticket.DeletedAt"/>.</summary>
    public static string TicketDeletedAtIndexName => "IX_Tickets_Archived";

    /// <summary>Gets the <see cref="Ticket.InternalName"/> length the application chooses.</summary>
    public static int TicketInternalNameMaxLength => 500;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Ticket>(ticket =>
        {
            ticket.Property(t => t.InternalName).HasMaxLength(TicketInternalNameMaxLength);
            ticket.HasIndex(t => t.DeletedAt).HasDatabaseName(TicketDeletedAtIndexName);
            ticket.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ImportedRecord>().Property(r => r.Id).ValueGeneratedNever();
        builder.Entity<Voucher>();
    }
}
