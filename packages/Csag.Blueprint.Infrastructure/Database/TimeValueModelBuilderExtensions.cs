namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Extension methods for the columns of the time values in <c>Csag.Blueprint.Domain.Time</c>:
/// <see cref="PlaceTime"/>, <see cref="ZonedPlaceTime"/> and <see cref="ViewerTime"/>.
/// <para>
/// EF Core maps a property of one of these types as a complex type, whose columns it names
/// <c>{Property}_{Member}</c>. These methods name them <c>{Property}Local</c> (<c>datetime2</c> on SQL Server),
/// <c>{Property}At</c> (<c>datetimeoffset</c>) and <c>{Property}TimeZoneId</c> (<c>nvarchar(100)</c>), so a
/// property <c>Start</c> is stored in <c>StartLocal</c> and <c>StartAt</c>. A property of an optional type
/// (<c>PlaceTime?</c>) gets nullable columns, which are all null when it is. Inside an owned type or another
/// complex type, the columns keep the prefix EF Core gives that type (<c>Venue_DoorsOpenLocal</c>).
/// </para>
/// <para>
/// Everything is configured at convention level, so a column the application names or sizes explicitly
/// (<c>ComplexProperty(e => e.Start, c => c.Property(t => t.Local).HasColumnName("Begins"))</c>) keeps its
/// configuration whether it runs before or after these methods. Time values mapped to JSON are left alone.
/// </para>
/// </summary>
public static class TimeValueModelBuilderExtensions
{
    /// <summary>
    /// The length of a <c>TimeZoneId</c> column; the longest id in the tz database has 32 characters.
    /// </summary>
    private const int TimeZoneIdMaxLength = 100;

    /// <summary>
    /// Names the columns of every <see cref="PlaceTime"/>, <see cref="ZonedPlaceTime"/> and
    /// <see cref="ViewerTime"/> property in the model after the property, and sets the length of their
    /// <c>TimeZoneId</c> columns.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureTimeValueColumns(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureTimeValueColumns(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Applies <see cref="ConfigureTimeValueColumns(ModelBuilder)"/> through a convention-level builder.
    /// </summary>
    /// <param name="modelBuilder">The convention-level builder of the model to configure.</param>
    internal static void ConfigureTimeValueColumns(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(entityType => !entityType.IsMappedToJson()))
        {
            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (storeObject is { } mappedTo)
            {
                ConfigureComplexProperties(entityType, mappedTo);
            }
        }
    }

    private static void ConfigureComplexProperties(IConventionTypeBase type, StoreObjectIdentifier storeObject)
    {
        foreach (var complexProperty in type.GetDeclaredComplexProperties())
        {
            var complexType = complexProperty.ComplexType;
            if (complexType.IsMappedToJson())
            {
                continue;
            }

            if (IsTimeValue(complexProperty.ClrType))
            {
                ConfigureTimeValue(complexType, storeObject);
            }
            else
            {
                ConfigureComplexProperties(complexType, storeObject);
            }
        }
    }

    private static void ConfigureTimeValue(IConventionComplexType timeValue, StoreObjectIdentifier storeObject)
    {
        foreach (var property in timeValue.GetDeclaredProperties())
        {
            // EF Core's name is the path to the member joined with underscores (Start_Local, or Venue_Start_Local
            // in an owned type sharing its owner's table); joining the member to the property without one keeps
            // the rest of the path.
            var defaultName = property.GetDefaultColumnName(storeObject);
            var memberSuffix = "_" + property.Name;
            if (defaultName?.EndsWith(memberSuffix, StringComparison.Ordinal) == true)
            {
                property.Builder.HasColumnName(defaultName[..^memberSuffix.Length] + property.Name);
            }
        }

        timeValue.FindProperty(nameof(ZonedPlaceTime.TimeZoneId))?.Builder.HasMaxLength(TimeZoneIdMaxLength);
    }

    private static bool IsTimeValue(Type type)
        => type == typeof(PlaceTime) || type == typeof(ZonedPlaceTime) || type == typeof(ViewerTime);
}
