namespace Csag.Blueprint.Application.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Tests the fluent column builder of <see cref="TableViewColumnDefinition{TEntity, TDto}"/>:
/// the operator-to-input-hint derivation, the humanized display name default, and the
/// guard clauses on the translation-key setters.
/// </summary>
public sealed class TableViewColumnDefinitionTests
{
    [Theory]
    [InlineData(TableViewFilterOperator.Equals, TableViewFilterInputHint.Text)]
    [InlineData(TableViewFilterOperator.Contains, TableViewFilterInputHint.Text)]
    [InlineData(TableViewFilterOperator.Boolean, TableViewFilterInputHint.Select)]
    [InlineData(TableViewFilterOperator.Enum, TableViewFilterInputHint.Select)]
    [InlineData(TableViewFilterOperator.In, TableViewFilterInputHint.Select)]
    [InlineData(TableViewFilterOperator.Range, TableViewFilterInputHint.NumberRange)]
    [InlineData(TableViewFilterOperator.DateRange, TableViewFilterInputHint.DateRange)]
    [InlineData((TableViewFilterOperator)999, TableViewFilterInputHint.Text)] // switch default falls back to Text
    public void Filterable_DerivesFilterInputHintFromOperator(TableViewFilterOperator filterOperator, TableViewFilterInputHint expected)
    {
        var definition = new ProbeDefinition();

        definition.NameColumn.Filterable(filterOperator);

        definition.MetadataFor("Name").FilterInputHint.ShouldBe(expected);
    }

    [Fact]
    public void Filterable_SetsOperatorAndAllowedValues()
    {
        var definition = new ProbeDefinition();

        definition.NameColumn.Filterable(TableViewFilterOperator.In, ["small", "large"]);

        var metadata = definition.MetadataFor("Name");
        metadata.IsFilterable.ShouldBeTrue();
        metadata.FilterOperator.ShouldBe(TableViewFilterOperator.In);
        metadata.AllowedValues.ShouldBe(["small", "large"]);
    }

    [Fact]
    public void WithFilterInputHint_OverridesTheDerivedHint()
    {
        var definition = new ProbeDefinition();

        definition.NameColumn
            .Filterable(TableViewFilterOperator.Equals)
            .WithFilterInputHint(TableViewFilterInputHint.Autocomplete);

        definition.MetadataFor("Name").FilterInputHint.ShouldBe(TableViewFilterInputHint.Autocomplete);
    }

    [Theory]
    [InlineData("Name", "Name")]
    [InlineData("PricePerHour", "Price Per Hour")]
    [InlineData("VATRate", "V A T Rate")] // every non-leading capital gets a space, so acronyms are split letter by letter
    public void DisplayName_DefaultsToHumanizedColumnName(string columnName, string expected)
    {
        var definition = new ProbeDefinition();

        definition.MetadataFor(columnName).DisplayName.ShouldBe(expected);
    }

    [Fact]
    public void WithDisplayName_ReplacesTheHumanizedDefault()
    {
        var definition = new ProbeDefinition();

        definition.PriceColumn.WithDisplayName("Hourly Rate");

        definition.MetadataFor("PricePerHour").DisplayName.ShouldBe("Hourly Rate");
    }

    [Fact]
    public void WithTranslatedDisplayName_RecordsKeyWithoutTouchingDisplayName()
    {
        var definition = new ProbeDefinition();

        definition.PriceColumn.WithTranslatedDisplayName("tables.machines.pricePerHour");

        var metadata = definition.MetadataFor("PricePerHour");
        metadata.DisplayNameKey.ShouldBe("tables.machines.pricePerHour");
        metadata.DisplayName.ShouldBe("Price Per Hour");
    }

    [Fact]
    public void WithTranslatedDisplayName_RejectsNullAndEmptyKeys()
    {
        var definition = new ProbeDefinition();

        Should.Throw<ArgumentNullException>(() => definition.NameColumn.WithTranslatedDisplayName(null!));
        Should.Throw<ArgumentException>(() => definition.NameColumn.WithTranslatedDisplayName(string.Empty));
    }

    [Fact]
    public void WithTranslatedDescription_RecordsKeyAndKeepsDescriptionAsFallback()
    {
        var definition = new ProbeDefinition();

        definition.NameColumn
            .WithDescription("The machine name")
            .WithTranslatedDescription("tables.machines.name.description");

        var metadata = definition.MetadataFor("Name");
        metadata.Description.ShouldBe("The machine name");
        metadata.DescriptionKey.ShouldBe("tables.machines.name.description");
    }

    [Fact]
    public void WithTranslatedDescription_RejectsNullAndEmptyKeys()
    {
        var definition = new ProbeDefinition();

        Should.Throw<ArgumentNullException>(() => definition.NameColumn.WithTranslatedDescription(null!));
        Should.Throw<ArgumentException>(() => definition.NameColumn.WithTranslatedDescription(string.Empty));
    }

    [Fact]
    public void Sortable_WithNullSortExpression_Throws()
    {
        var definition = new ProbeDefinition();

        Should.Throw<ArgumentNullException>(() => definition.NameColumn.Sortable<string>(null!));
    }

    [Fact]
    public void EnumColumn_AutoPopulatesAllowedValuesFromEnumNames()
    {
        var definition = new ProbeDefinition();

        var metadata = definition.MetadataFor("Direction");
        metadata.DataType.ShouldBe("enum");
        metadata.AllowedValues.ShouldBe(["Asc", "Desc"]);
    }

    /// <summary>
    /// The entity shape is only ever inspected through property-accessor expressions and never
    /// materialized, so every property carries an initializer to count as assigned.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Referenced via generic table view expressions.")]
    private sealed class Machine
    {
        public string Name { get; set; } = string.Empty;

        public decimal PricePerHour { get; set; } = 1m;

        public decimal VatRate { get; set; } = 1m;

        public SortDirection Direction { get; set; } = SortDirection.Desc;
    }

    /// <summary>
    /// The DTO shape is only ever inspected through property-accessor expressions and never
    /// materialized, so every property carries an initializer to count as assigned.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Referenced via generic table view expressions.")]
    private sealed class MachineDto
    {
        public string Name { get; set; } = string.Empty;

        public decimal PricePerHour { get; set; } = 1m;

        public decimal VatRate { get; set; } = 1m;

        public SortDirection Direction { get; set; } = SortDirection.Desc;
    }

    private sealed class ProbeDefinition : TableViewDefinition<Machine, MachineDto>
    {
        public ProbeDefinition()
        {
            this.NameColumn = this.Column(d => d.Name);
            this.PriceColumn = this.Column(d => d.PricePerHour);

            // A computed column can carry an arbitrary name, which lets the humanization tests
            // cover an acronym-style column name without an equally named property.
            this.ComputedColumn("VATRate", e => e.VatRate, d => d.VatRate);

            // SortDirection is reused as an arbitrary enum so the test file does not need its own.
            this.Column(d => d.Direction);
        }

        public TableViewColumnDefinition<Machine, MachineDto> NameColumn { get; }

        public TableViewColumnDefinition<Machine, MachineDto> PriceColumn { get; }

        public TableViewColumnMetadata MetadataFor(string columnName)
        {
            return this.Metadata.Single(m => m.Name == columnName);
        }
    }
}
