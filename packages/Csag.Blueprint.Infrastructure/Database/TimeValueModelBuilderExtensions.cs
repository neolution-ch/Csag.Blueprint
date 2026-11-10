namespace Csag.Blueprint.Infrastructure.Database;

using Csag.Blueprint.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Extension methods for the columns of the time values in <c>Csag.Blueprint.Domain.Time</c>
/// (<see cref="PlaceTime"/> and <see cref="ZonedTime"/>) and of every instant.
/// <para>
/// EF Core maps a property of a time value type as a complex type, whose columns it names
/// <c>{Property}_{Member}</c>. These methods name them <c>{Property}Local</c> (<c>datetime2</c> on SQL Server),
/// <c>{Property}At</c> (<c>datetimeoffset</c>) and <c>{Property}TimeZoneId</c> (<c>nvarchar(100)</c>), so a
/// property <c>Start</c> is stored in <c>StartLocal</c> and <c>StartAt</c>. A property of an optional type
/// (<c>PlaceTime?</c>) gets nullable columns, which are all null when it is. Inside an owned type or another
/// complex type, the columns keep the prefix EF Core gives that type (<c>Venue_DoorsOpenLocal</c>).
/// </para>
/// <para>
/// Every <see cref="DateTimeOffset"/> property in the model, of an entity type or of a complex type such as
/// <see cref="PlaceTime.At"/>, is written at offset zero (<see cref="DateTimeOffset.ToUniversalTime"/>) and read
/// back as stored. Npgsql writes a <see cref="DateTimeOffset"/> to a <c>timestamptz</c> column only at offset
/// zero and throws for any other, and on SQL Server every stored instant then reads back as <c>+00:00</c>,
/// whatever offset it was written with. A query parameter compared with such a property is converted the
/// same way, so a filter written with another offset still matches the same instants.
/// </para>
/// <para>
/// Everything is configured at convention level, so a column the application names or sizes explicitly
/// (<c>ComplexProperty(e => e.Start, c => c.Property(t => t.Local).HasColumnName("Begins"))</c>) keeps its
/// configuration whether it runs before or after these methods, and a property with a value converter of its
/// own keeps that converter. Time values mapped to JSON keep the JSON mapping's names.
/// </para>
/// </summary>
public static class TimeValueModelBuilderExtensions
{
    /// <summary>
    /// The length of a <c>TimeZoneId</c> column; the longest id in the tz database has 32 characters.
    /// </summary>
    private const int TimeZoneIdMaxLength = 100;

    /// <summary>
    /// Writes an instant at offset zero and reads it back as stored. EF Core never passes it a null, so it
    /// serves nullable properties too.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcInstantConverter =
        new(instant => instant.ToUniversalTime(), instant => instant);

    /// <summary>
    /// Names the columns of every <see cref="PlaceTime"/> and <see cref="ZonedTime"/> property in the model
    /// after the property, sets the length of their <c>TimeZoneId</c> columns, and stores every
    /// <see cref="DateTimeOffset"/> property at offset zero.
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
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            ConfigureUtcInstants(entityType);

            if (entityType.IsMappedToJson())
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (storeObject is { } mappedTo)
            {
                ConfigureComplexProperties(entityType, mappedTo);
            }
        }
    }

    private static void ConfigureUtcInstants(IConventionTypeBase type)
    {
        foreach (var property in type.GetDeclaredProperties())
        {
            if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset)
                && property.GetValueConverter() is null
                && property.GetProviderClrType() is null)
            {
                property.Builder.HasConversion(UtcInstantConverter);
            }
        }

        foreach (var complexProperty in type.GetDeclaredComplexProperties())
        {
            ConfigureUtcInstants(complexProperty.ComplexType);
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

        timeValue.FindProperty(nameof(ZonedTime.TimeZoneId))?.Builder.HasMaxLength(TimeZoneIdMaxLength);
    }

    private static bool IsTimeValue(Type type)
        => type == typeof(PlaceTime) || type == typeof(ZonedTime);
}
