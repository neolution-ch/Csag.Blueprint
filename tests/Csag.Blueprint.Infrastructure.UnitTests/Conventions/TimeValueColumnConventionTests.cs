namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// A <see cref="PlaceTime"/>, <see cref="ZonedPlaceTime"/> or <see cref="ViewerTime"/> property is mapped as
/// a complex type without any configuration, and the blueprint conventions name its columns after the
/// property: <c>StartLocal</c>, <c>StartAt</c>, <c>StartTimeZoneId</c>.
/// </summary>
public sealed class TimeValueColumnConventionTests
{
    [Theory]
    [InlineData(nameof(Appointment.Start))]
    [InlineData(nameof(Appointment.StandOpens))]
    [InlineData(nameof(Appointment.Briefing))]
    public void RequiredTimeValue_IsStoredInNonNullableColumnsNamedAfterTheProperty(string property)
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        Column(table, $"{property}Local").StoreType.ShouldBe("datetime2");
        Column(table, $"{property}At").StoreType.ShouldBe("datetimeoffset");
        table.Columns.Where(c => c.Name.StartsWith(property, StringComparison.Ordinal)).ShouldAllBe(c => !c.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Appointment.End))]
    [InlineData(nameof(Appointment.StandCloses))]
    public void OptionalTimeValue_IsStoredInNullableColumns(string property)
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        Column(table, $"{property}Local").IsNullable.ShouldBeTrue();
        Column(table, $"{property}At").IsNullable.ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(Appointment.StandOpens), false)]
    [InlineData(nameof(Appointment.StandCloses), true)]
    [InlineData(nameof(Appointment.Briefing), false)]
    [InlineData(nameof(Appointment.Debriefing), true)]
    public void ZoneOfZonedTimeValue_IsStoredAsAnIdOfAtMost100Characters(string property, bool isNullable)
    {
        using var context = Build();

        var column = Column(AppointmentsTable(context), $"{property}TimeZoneId");

        column.StoreType.ShouldBe("nvarchar(100)");
        column.IsNullable.ShouldBe(isNullable);
    }

    [Fact]
    public void PlaceTime_HasNoZoneColumn()
    {
        using var context = Build();

        AppointmentsTable(context).FindColumn("StartTimeZoneId").ShouldBeNull();
    }

    [Fact]
    public void ExplicitColumnName_WinsOverTheConvention()
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        table.FindColumn(TimeValueDbContext.DebriefingLocalColumnName).ShouldNotBeNull();
        table.FindColumn("DebriefingLocal").ShouldBeNull();
        table.FindColumn("DebriefingAt").ShouldNotBeNull("the members the application left unconfigured keep the convention's names");
    }

    [Fact]
    public void TimeValueInAnotherComplexType_ContinuesThatTypesColumnNames()
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        table.FindColumn("Slot_OpensLocal").ShouldNotBeNull();
        table.FindColumn("Slot_OpensAt").ShouldNotBeNull();
        table.FindColumn("Slot_Room").ShouldNotBeNull("the application type's own columns keep EF Core's names");
    }

    [Fact]
    public void TimeValueInAnOwnedType_KeepsTheOwnedTypesPrefix()
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        table.FindColumn("Venue_DoorsOpenLocal").ShouldNotBeNull();
        table.FindColumn("Venue_DoorsOpenAt").ShouldNotBeNull();
        table.FindColumn("DoorsOpenLocal").ShouldBeNull();
    }

    [Fact]
    public void TimeValueMappedToJson_IsLeftToTheJsonMapping()
    {
        using var context = Build();
        var table = AppointmentsTable(context);

        table.FindColumn(nameof(Appointment.Archive)).ShouldNotBeNull();
        table.Columns.ShouldNotContain(c => c.Name.StartsWith("Archive_", StringComparison.Ordinal) || c.Name.StartsWith("ArchiveOpens", StringComparison.Ordinal));
    }

    [Fact]
    public void ConfigureTimeValueColumns_NamesTheColumnsOfAContextNotDerivedFromBlueprintDbContext()
    {
        using var context = new PlainTimeValueDbContext(
            new DbContextOptionsBuilder<PlainTimeValueDbContext>()
                .UseSqlServer("Server=nowhere;Database=time-values;Trusted_Connection=True;")
                .Options);

        var table = AppointmentsTable(context);

        Column(table, "StartLocal").StoreType.ShouldBe("datetime2");
        Column(table, "StandOpensTimeZoneId").StoreType.ShouldBe("nvarchar(100)");
    }

    [Fact]
    public void IndexOverATimeValueMember_IsNotSupportedByEfCore()
    {
        // EF Core 10 cannot index a property of a complex type. Applications create the index on the column in
        // their migration instead (migrationBuilder.CreateIndex(..., column: "StartAt")). When this starts to
        // pass through, HasIndex(e => e.Start.At) works and the documented workaround can go.
        var builder = new ModelBuilder();

        Should.Throw<ArgumentException>(() => builder.Entity<Appointment>().HasIndex(a => a.Start.At))
            .Message.ShouldContain("is not a valid member access expression");
    }

    // SQL Server, so the store types are the ones applications get. Building a model opens no connection,
    // so the server is never reached.
    private static TimeValueDbContext Build()
        => new(new DbContextOptionsBuilder<TimeValueDbContext>()
            .UseSqlServer("Server=nowhere;Database=time-values;Trusted_Connection=True;")
            .Options);

    private static ITable AppointmentsTable(DbContext context)
        => context.Model.GetRelationalModel().FindTable("Appointments", schema: null)!;

    private static IColumn Column(ITable table, string name)
        => table.FindColumn(name) ?? throw new InvalidOperationException($"The table has no column '{name}'.");
}
