namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Every <see cref="DateTimeOffset"/> property, of an entity or of a complex type such as
/// <see cref="PlaceTime.At"/>, is written at offset zero and read back as stored, unless the application
/// converts it itself.
/// </summary>
public sealed class UtcInstantConventionTests
{
    private static readonly DateTimeOffset AtPlusTwo = new(2026, 7, 1, 10, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(nameof(Appointment.BookedAt))]
    [InlineData(nameof(Appointment.CancelledAt))]
    [InlineData("Start.At")]
    [InlineData("End.At")]
    [InlineData("StandOpens.At")]
    [InlineData("Debriefing.At")]
    [InlineData("Slot.Opens.At")]
    [InlineData("Archive.Opens.At")]
    [InlineData("Venue.DoorsOpen.At")]
    public void Instant_IsWrittenAtOffsetZeroAndReadBackAsStored(string path)
    {
        using var context = Build();

        ShouldConvertToUtc(FindProperty(context, path));
    }

    [Theory]
    [InlineData("Start.Local")]
    [InlineData("StandOpens.Local")]
    public void LocalReading_IsNotConverted(string path)
    {
        using var context = Build();

        FindProperty(context, path).GetTypeMapping().Converter.ShouldBeNull();
    }

    [Fact]
    public void InstantWithAConverterOfItsOwn_KeepsIt()
    {
        using var context = Build();
        var table = context.Model.GetRelationalModel().FindTable("Appointments", schema: null)!;

        FindProperty(context, nameof(Appointment.RemindedAt)).GetValueConverter().ShouldBeSameAs(TimeValueDbContext.RemindedAtConverter);
        table.FindColumn(nameof(Appointment.RemindedAt))!.StoreType.ShouldBe("bigint");

        var exportedAt = FindProperty(context, nameof(Appointment.ExportedAt)).GetTypeMapping().Converter.ShouldNotBeNull();
        exportedAt.ConvertToProvider(AtPlusTwo).ShouldBeOfType<string>().ShouldEndWith("+02:00", Case.Sensitive, "the application's conversion to text keeps the offset");
    }

    [Theory]
    [InlineData(nameof(Appointment.BookedAt))]
    [InlineData(nameof(Appointment.CancelledAt))]
    [InlineData(nameof(Appointment.RemindedAt))]
    [InlineData("Start.At")]
    [InlineData("Venue.DoorsOpen.At")]
    public void ConfigureTimeValueColumns_ConvertsTheInstantsOfAContextNotDerivedFromBlueprintDbContext(string path)
    {
        using var context = new PlainTimeValueDbContext(
            new DbContextOptionsBuilder<PlainTimeValueDbContext>()
                .UseSqlServer("Server=nowhere;Database=time-values;Trusted_Connection=True;")
                .Options);

        ShouldConvertToUtc(FindProperty(context, path));
    }

    // SQL Server, so the type mappings are the ones applications get. Building a model opens no connection,
    // so the server is never reached.
    private static TimeValueDbContext Build()
        => new(new DbContextOptionsBuilder<TimeValueDbContext>()
            .UseSqlServer("Server=nowhere;Database=time-values;Trusted_Connection=True;")
            .Options);

    private static void ShouldConvertToUtc(IProperty property)
    {
        // The type mapping is what EF Core writes and reads with, so the converter has to have reached it.
        var converter = property.GetTypeMapping().Converter.ShouldNotBeNull();

        var written = converter.ConvertToProvider(AtPlusTwo).ShouldBeOfType<DateTimeOffset>();
        written.ShouldBe(AtPlusTwo, "the same instant");
        written.Offset.ShouldBe(TimeSpan.Zero);

        converter.ConvertFromProvider(AtPlusTwo).ShouldBeOfType<DateTimeOffset>().Offset.ShouldBe(TimeSpan.FromHours(2), "values are read back as stored");
    }

    private static IProperty FindProperty(DbContext context, string path)
    {
        ITypeBase type = context.Model.FindEntityType(typeof(Appointment))!;
        var segments = path.Split('.');
        foreach (var segment in segments[..^1])
        {
            type = (ITypeBase?)type.FindComplexProperty(segment)?.ComplexType
                ?? ((IEntityType)type).FindNavigation(segment)?.TargetEntityType
                ?? throw new InvalidOperationException($"'{type.DisplayName()}' has no complex property or navigation '{segment}'.");
        }

        return type.FindProperty(segments[^1]) ?? throw new InvalidOperationException($"'{type.DisplayName()}' has no property '{segments[^1]}'.");
    }
}
