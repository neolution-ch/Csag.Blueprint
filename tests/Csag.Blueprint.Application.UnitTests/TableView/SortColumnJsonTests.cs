namespace Csag.Blueprint.Application.UnitTests.TableView;

using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;

/// <summary>
/// Pins down the JSON contract for <see cref="SortColumn"/> / <see cref="SortDirection"/>:
/// case-insensitive on the way in, PascalCase on the way out. Uses the production
/// <see cref="BlueprintJsonOptions.Default"/> instance so this test fails (rather than
/// silently drifting) if someone changes the converter or naming policy in shared config.
/// </summary>
public sealed class SortColumnJsonTests
{
    private static readonly JsonSerializerOptions Options = BlueprintJsonOptions.Default;

    [Theory]
    [InlineData("asc", SortDirection.Asc)]
    [InlineData("Asc", SortDirection.Asc)]
    [InlineData("ASC", SortDirection.Asc)]
    [InlineData("desc", SortDirection.Desc)]
    [InlineData("Desc", SortDirection.Desc)]
    [InlineData("DESC", SortDirection.Desc)]
    public void Deserialize_DirectionCasingIsIgnored(string raw, SortDirection expected)
    {
        // Arrange
        var json = $"{{\"columnName\":\"Name\",\"direction\":\"{raw}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<SortColumn>(json, Options);

        // Assert
        result.ShouldNotBeNull();
        result.ColumnName.ShouldBe("Name");
        result.Direction.ShouldBe(expected);
    }

    [Theory]
    [InlineData(SortDirection.Asc, "Asc")]
    [InlineData(SortDirection.Desc, "Desc")]
    public void Serialize_DirectionUsesPascalCase(SortDirection direction, string expected)
    {
        // Arrange
        var column = new SortColumn { ColumnName = "Name", Direction = direction };

        // Act
        var json = JsonSerializer.Serialize(column, Options);

        // Assert
        json.ShouldContain($"\"direction\":\"{expected}\"");
    }
}
