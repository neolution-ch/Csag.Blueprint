namespace Csag.Blueprint.Application.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Tests the derived <see cref="TableViewDataResult{TDto}.TotalPages"/> value: ceiling division
/// of the total count by the page size, guarded to zero for non-positive page sizes.
/// </summary>
public sealed class TableViewDataResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(95, 10, 10)]
    [InlineData(7, 1, 7)]
    [InlineData(7, 0, 0)]
    [InlineData(7, -5, 0)]
    public void TotalPages_UsesCeilingDivisionAndGuardsNonPositivePageSize(int totalCount, int pageSize, int expected)
    {
        var result = new TableViewDataResult<string>
        {
            TotalCount = totalCount,
            PageSize = pageSize,
        };

        result.TotalPages.ShouldBe(expected);
    }
}
