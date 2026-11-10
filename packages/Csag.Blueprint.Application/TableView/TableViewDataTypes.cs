namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// The values of <see cref="TableViewColumnMetadata.DataType"/>, derived from the CLR type of the
/// column's DTO property.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Each name mirrors the data type value it holds, and several of those values are type names.")]
public static class TableViewDataTypes
{
    /// <summary>
    /// A <see cref="string"/>, and any type without a more specific data type.
    /// </summary>
    public static readonly string String = "string";

    /// <summary>
    /// A numeric type such as <see cref="int"/> or <see cref="decimal"/>.
    /// </summary>
    public static readonly string Number = "number";

    /// <summary>
    /// A <see cref="bool"/>.
    /// </summary>
    public static readonly string Boolean = "boolean";

    /// <summary>
    /// A calendar date, <see cref="DateOnly"/>: a day with no time and no zone, sent as <c>yyyy-MM-dd</c>.
    /// </summary>
    public static readonly string Date = "date";

    /// <summary>
    /// A local reading, <see cref="DateTime"/>: the clock reading at a place, sent as
    /// <c>yyyy-MM-ddTHH:mm:ss</c> without an offset and shown as it is.
    /// </summary>
    public static readonly string LocalDateTime = "localDateTime";

    /// <summary>
    /// An instant, <see cref="DateTimeOffset"/>: when something happened, sent in UTC and shown in the
    /// viewer's zone.
    /// </summary>
    public static readonly string Instant = "instant";

    /// <summary>
    /// An enum, filtered and labelled through <see cref="TableViewColumnMetadata.AllowedValues"/>.
    /// </summary>
    public static readonly string Enum = "enum";

    /// <summary>
    /// A <see cref="System.Guid"/>.
    /// </summary>
    public static readonly string Guid = "guid";
}
