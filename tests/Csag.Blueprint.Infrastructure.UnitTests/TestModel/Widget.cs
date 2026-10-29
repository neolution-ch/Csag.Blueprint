namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using System.Globalization;

/// <summary>
/// Test entity whose <see cref="Guid"/> key already declares an explicit CLR default value, so the
/// key convention must leave it alone rather than adding <c>NEWSEQUENTIALID()</c> on top.
/// </summary>
public sealed class Widget
{
    /// <summary>The explicitly configured key default.</summary>
    public static readonly Guid SeededId = Guid.Parse("11111111-1111-1111-1111-111111111111", CultureInfo.InvariantCulture);

    public Guid Id { get; set; }
}
