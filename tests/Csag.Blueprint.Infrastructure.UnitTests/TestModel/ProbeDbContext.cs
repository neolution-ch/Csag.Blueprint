namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Minimal context that applies the blueprint conventions to a model which follows none of their
/// naming assumptions: an unconventional localized-text foreign key and a key with a CLR default.
/// </summary>
public sealed class ProbeDbContext : DbContext
{
    public ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Widget> Widgets => this.Set<Widget>();

    public DbSet<Gadget> Gadgets => this.Set<Gadget>();

    public DbSet<GadgetText> GadgetTexts => this.Set<GadgetText>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Widget>().Property(w => w.Id).HasDefaultValue(Widget.SeededId);
        modelBuilder.Entity<GadgetText>().HasOne(t => t.Owner).WithMany(g => g.LocalizedTexts).HasForeignKey(t => t.OwnerId);

        modelBuilder.ConfigureLocalizedTextConventions();
        modelBuilder.ConfigureGuidPrimaryKeyDefaults();
    }
}
