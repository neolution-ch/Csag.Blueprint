namespace Csag.Blueprint.Application.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Tests nullable column registration for the typed TableView API.
/// </summary>
public sealed class NullableColumnDefinitionTests
{
    [Fact]
    public void Metadata_InfersNullableDateColumnAsDate()
    {
        var definition = new TestNullableTableViewDefinition();

        var column = definition.Metadata.Single(x => x.Name == nameof(TestDto.NullableDate));

        column.DataType.ShouldBe("date");
        column.IsSortable.ShouldBeTrue();
        column.IsFilterable.ShouldBeTrue();
    }

    [Fact]
    public void Metadata_InfersNullableGuidColumnAsGuid()
    {
        var definition = new TestNullableTableViewDefinition();

        var column = definition.Metadata.Single(x => x.Name == nameof(TestDto.NullableGuid));

        column.DataType.ShouldBe("guid");
        column.IsSortable.ShouldBeTrue();
        column.IsFilterable.ShouldBeTrue();
    }

    [Fact]
    public void GetSortExpression_ForNullableDateColumn_ReturnsTypedExpression()
    {
        var definition = new TestNullableTableViewDefinition();

        var sortExpression = definition.GetSortExpression(nameof(TestDto.NullableDate));

        sortExpression.ShouldNotBeNull();
        sortExpression.Value.Expression.ReturnType.ShouldBe(typeof(DateOnly?));
    }

    [Fact]
    public void GetSortExpression_ForCustomNullableDateSort_ReturnsTypedFallbackExpression()
    {
        var definition = new TestCustomSortTableViewDefinition();

        var sortExpression = definition.GetSortExpression(nameof(TestDto.NullableDate));

        sortExpression.ShouldNotBeNull();
        sortExpression.Value.Expression.ReturnType.ShouldBe(typeof(DateOnly));
    }

    [Fact]
    public void GetFilterExpression_ForNullableGuidColumn_ReturnsExpression()
    {
        var definition = new TestNullableTableViewDefinition();
        var value = Guid.NewGuid();

        var filterExpression = definition.GetFilterExpression(nameof(TestDto.NullableGuid), value.ToString());

        filterExpression.ShouldNotBeNull();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via generic table view expressions.")]
    private sealed class TestEntity(DateOnly? nullableDate, Guid? nullableGuid)
    {
        public DateOnly? NullableDate { get; } = nullableDate;

        public Guid? NullableGuid { get; } = nullableGuid;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via expression analysis in tests.")]
    private sealed class TestDto(DateOnly? nullableDate, Guid? nullableGuid)
    {
        public DateOnly? NullableDate { get; } = nullableDate;

        public Guid? NullableGuid { get; } = nullableGuid;
    }

    private sealed class TestNullableTableViewDefinition : TableViewDefinition<TestEntity, TestDto>
    {
        public TestNullableTableViewDefinition()
        {
            this.Column(d => d.NullableDate, e => e.NullableDate)
                .Filterable(TableViewFilterOperator.DateRange)
                .Sortable();

            this.Column(d => d.NullableGuid, e => e.NullableGuid)
                .Filterable(TableViewFilterOperator.Equals)
                .Sortable();
        }
    }

    private sealed class TestCustomSortTableViewDefinition : TableViewDefinition<TestEntity, TestDto>
    {
        public TestCustomSortTableViewDefinition()
        {
            this.Column(d => d.NullableDate, e => e.NullableDate)
                .Filterable(TableViewFilterOperator.DateRange)
                .Sortable(e => e.NullableDate ?? DateOnly.MinValue);
        }
    }
}
