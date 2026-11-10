namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Context that does not derive from <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}"/> and
/// applies the time value column names itself.
/// </summary>
public sealed class PlainTimeValueDbContext : DbContext
{
    public PlainTimeValueDbContext(DbContextOptions<PlainTimeValueDbContext> options)
        : base(options)
    {
    }

    public DbSet<Appointment> Appointments => this.Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Appointment>(appointment =>
        {
            appointment.ComplexProperty(a => a.Archive, archive => archive.ToJson());
            appointment.OwnsOne(a => a.Venue);
        });
        modelBuilder.ConfigureTimeValueColumns();
    }
}
