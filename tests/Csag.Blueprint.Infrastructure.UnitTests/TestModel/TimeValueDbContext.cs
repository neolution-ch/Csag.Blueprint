namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Application-shaped context with an entity holding time values, one of whose columns the application
/// names itself, and instants, two of which the application converts itself.
/// </summary>
public sealed class TimeValueDbContext : BlueprintDbContext<TestTenant, TestUser, TestRole>
{
    public TimeValueDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Gets the column name the application chooses for the reading of <see cref="Appointment.Debriefing"/>.</summary>
    public static string DebriefingLocalColumnName => "DebriefingStartsLocally";

    /// <summary>Gets the converter the application chooses for <see cref="Appointment.RemindedAt"/>.</summary>
    public static ValueConverter<DateTimeOffset, long> RemindedAtConverter { get; } = new DateTimeOffsetToBinaryConverter();

    public DbSet<Appointment> Appointments => this.Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<Appointment>(appointment =>
        {
            appointment.ComplexProperty(a => a.Debriefing, debriefing => debriefing.Property(t => t.Local).HasColumnName(DebriefingLocalColumnName));
            appointment.ComplexProperty(a => a.Archive, archive => archive.ToJson());
            appointment.OwnsOne(a => a.Venue);
            appointment.Property(a => a.RemindedAt).HasConversion(RemindedAtConverter);
            appointment.Property(a => a.ExportedAt).HasConversion<string>();
        });
    }
}
