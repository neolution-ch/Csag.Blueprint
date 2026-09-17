namespace Csag.Blueprint.Application.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Tests the Projection composed by <see cref="TableViewDefinition{TEntity, TDto}"/>: only columns
/// with both a DTO and an entity expression are bound, and each binding is wrapped in a type
/// conversion so lifted (nullable) DTO properties map from non-nullable entity members.
/// </summary>
public sealed class TableViewProjectionTests
{
    [Fact]
    public void Projection_MapsEntityValuesOntoDtoProperties()
    {
        var definition = new WidgetTableViewDefinition();
        var widget = new Widget { Name = "gear", Quantity = 7 };

        var dto = definition.Projection.Compile()(widget);

        dto.Name.ShouldBe("gear");
    }

    [Fact]
    public void Projection_ConvertsEntityValueToNullableDtoProperty()
    {
        var definition = new WidgetTableViewDefinition();
        var widget = new Widget { Name = "gear", Quantity = 7 };

        var dto = definition.Projection.Compile()(widget);

        dto.OptionalQuantity.ShouldBe(7);
    }

    [Fact]
    public void Projection_MapsComputedColumns()
    {
        var definition = new WidgetTableViewDefinition();
        var widget = new Widget { Name = "gear", Quantity = 7 };

        var dto = definition.Projection.Compile()(widget);

        dto.Label.ShouldBe("gear (7)");
    }

    [Fact]
    public void Projection_SkipsColumnsWithoutEntityExpression()
    {
        var definition = new WidgetTableViewDefinition();
        var widget = new Widget { Name = "gear", Quantity = 7 };

        var dto = definition.Projection.Compile()(widget);

        // Synthetic has no matching entity property, so the projection must leave the
        // DTO's own initializer value untouched instead of binding anything.
        dto.Synthetic.ShouldBe("unset");
    }

    private sealed class Widget
    {
        public string Name { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the compiled projection expression.")]
    private sealed class WidgetDto
    {
        public string Name { get; set; } = string.Empty;

        public int? OptionalQuantity { get; set; } = -1;

        public string Label { get; set; } = string.Empty;

        public string Synthetic { get; set; } = "unset";
    }

    private sealed class WidgetTableViewDefinition : TableViewDefinition<Widget, WidgetDto>
    {
        public WidgetTableViewDefinition()
        {
            this.Column(d => d.Name);
            this.Column(d => d.OptionalQuantity, e => e.Quantity);
            this.ComputedColumn("Label", e => e.Name + " (" + e.Quantity + ")", d => d.Label);
            this.Column(d => d.Synthetic);
        }
    }
}
