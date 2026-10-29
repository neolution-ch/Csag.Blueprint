namespace Csag.Blueprint.Application.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Tests the fluent column builder of <see cref="TableViewColumnDefinition{TEntity, TDto}"/>:
/// the operator-to-input-hint derivation, the humanized display name default, the allowed values
/// derived for enum columns and set through the fluent API, and the guard clauses on the
/// translation-key setters.
/// </summary>
public sealed class TableViewColumnDefinitionTests
{
    /// <summary>
    /// Member names cover the label humanization cases: word boundaries and acronyms.
    /// </summary>
    private enum PaymentMethod
    {
        /// <summary>
        /// Two words.
        /// </summary>
        CreditCard,

        /// <summary>
        /// Two words.
        /// </summary>
        BankTransfer,

        /// <summary>
        /// An acronym followed by a word.
        /// </summary>
        SMSCode,

        /// <summary>
        /// An acronym alone.
        /// </summary>
        IBAN,
    }

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
        metadata.AllowedValues.ShouldBe([new TableViewAllowedValue("small", "small"), new TableViewAllowedValue("large", "large")]);
    }

    [Fact]
    public void Filterable_WithoutAllowedValues_KeepsTheValuesDerivedForEnumColumns()
    {
        var definition = new ProbeDefinition();

        definition.PaymentColumn.Filterable(TableViewFilterOperator.Enum);

        var allowedValues = definition.MetadataFor("Payment").AllowedValues.ShouldNotBeNull();
        allowedValues.Select(v => v.Label).ShouldBe(["Credit card", "Bank transfer", "SMS code", "IBAN"]);
        allowedValues.ShouldAllBe(v => v.LabelKey != null && v.IsLabelKeyOptional);
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
    public void EnumColumn_DerivesMemberNamesAsValuesAndHumanizedLabels()
    {
        var definition = new ProbeDefinition();

        var metadata = definition.MetadataFor("Payment");
        metadata.DataType.ShouldBe("enum");
        var allowedValues = metadata.AllowedValues.ShouldNotBeNull();
        allowedValues.Select(v => v.Value).ShouldBe(["CreditCard", "BankTransfer", "SMSCode", "IBAN"]);
        allowedValues.Select(v => v.Label).ShouldBe(["Credit card", "Bank transfer", "SMS code", "IBAN"]);
    }

    [Fact]
    public void EnumColumn_DerivesOptionalConventionLabelKeys()
    {
        var definition = new ProbeDefinition();

        var allowedValues = definition.MetadataFor("Payment").AllowedValues.ShouldNotBeNull();

        allowedValues.Select(v => v.LabelKey).ShouldBe(
        [
            "Enums.PaymentMethod.CreditCard",
            "Enums.PaymentMethod.BankTransfer",
            "Enums.PaymentMethod.SMSCode",
            "Enums.PaymentMethod.IBAN",
        ]);
        allowedValues.ShouldAllBe(v => v.IsLabelKeyOptional);
    }

    [Fact]
    public void NullableEnumColumn_DerivesAllowedValuesFromTheUnderlyingEnum()
    {
        var definition = new ProbeDefinition();

        var allowedValues = definition.MetadataFor("OptionalPayment").AllowedValues.ShouldNotBeNull();

        allowedValues.Select(v => v.Value).ShouldBe(["CreditCard", "BankTransfer", "SMSCode", "IBAN"]);
        allowedValues[0].LabelKey.ShouldBe("Enums.PaymentMethod.CreditCard");
    }

    [Fact]
    public void WithAllowedValues_Strings_ServesEachValueAsItsOwnLabelWithoutKey()
    {
        var definition = new ProbeDefinition();

        // Plain strings are not humanized: an arbitrary value is not an identifier.
        definition.PaymentColumn.WithAllowedValues(["CreditCard", "BankTransfer"]);

        definition.MetadataFor("Payment").AllowedValues.ShouldBe(
            [new TableViewAllowedValue("CreditCard", "CreditCard"), new TableViewAllowedValue("BankTransfer", "BankTransfer")]);
    }

    [Fact]
    public void WithAllowedValues_LabeledValues_KeepsLabelsAndKeys()
    {
        var definition = new ProbeDefinition();
        TableViewAllowedValue[] values =
        [
            new("S", "Small") { LabelKey = "Sizes.Small" },
            new("L", "Large"),
        ];

        definition.NameColumn.WithAllowedValues(values);

        definition.MetadataFor("Name").AllowedValues.ShouldBe(values);
    }

    [Fact]
    public void WithAllowedValues_CopiesTheValuesIntoAReadOnlyList()
    {
        var definition = new ProbeDefinition();
        var values = new[] { new TableViewAllowedValue("S", "Small") };

        definition.NameColumn.WithAllowedValues(values);
        values[0] = new TableViewAllowedValue("X", "Replaced");

        var allowedValues = definition.MetadataFor("Name").AllowedValues.ShouldNotBeNull();
        allowedValues.Single().Value.ShouldBe("S");
        allowedValues.ShouldBeAssignableTo<ICollection<TableViewAllowedValue>>().IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void WithAllowedValues_RejectsNullCollectionsAndEntries()
    {
        var definition = new ProbeDefinition();

        Should.Throw<ArgumentNullException>(() => definition.NameColumn.WithAllowedValues((IEnumerable<string>)null!));
        Should.Throw<ArgumentNullException>(() => definition.NameColumn.WithAllowedValues((IEnumerable<TableViewAllowedValue>)null!));
        Should.Throw<ArgumentException>(() => definition.NameColumn.WithAllowedValues([new TableViewAllowedValue("S", "Small"), null!]));
    }

    [Fact]
    public void WithValueLabelKeyPrefix_KeysEveryValueAndMarksTheKeysRequired()
    {
        var definition = new ProbeDefinition();

        definition.NameColumn
            .Filterable(TableViewFilterOperator.In, ["small", "large"])
            .WithValueLabelKeyPrefix("Sizes");

        var allowedValues = definition.MetadataFor("Name").AllowedValues.ShouldNotBeNull();
        allowedValues.Select(v => v.LabelKey).ShouldBe(["Sizes.small", "Sizes.large"]);
        allowedValues.Select(v => v.Label).ShouldBe(["small", "large"]);
        allowedValues.ShouldAllBe(v => !v.IsLabelKeyOptional);
    }

    [Fact]
    public void WithValueLabelKeyPrefix_OnEnumColumn_ReplacesTheConventionKeysAndKeepsHumanizedLabels()
    {
        var definition = new ProbeDefinition();

        definition.PaymentColumn.WithValueLabelKeyPrefix("Payments.Methods");

        var allowedValues = definition.MetadataFor("Payment").AllowedValues.ShouldNotBeNull();
        allowedValues.Select(v => v.LabelKey).ShouldBe(
        [
            "Payments.Methods.CreditCard",
            "Payments.Methods.BankTransfer",
            "Payments.Methods.SMSCode",
            "Payments.Methods.IBAN",
        ]);
        allowedValues.Select(v => v.Label).ShouldBe(["Credit card", "Bank transfer", "SMS code", "IBAN"]);
        allowedValues.ShouldAllBe(v => !v.IsLabelKeyOptional);
    }

    [Fact]
    public void WithValueLabelKeyPrefix_WithoutAllowedValues_Throws()
    {
        var definition = new ProbeDefinition();

        Should.Throw<InvalidOperationException>(() => definition.NameColumn.WithValueLabelKeyPrefix("Sizes"));
    }

    [Fact]
    public void WithValueLabelKeyPrefix_RejectsNullAndEmptyPrefixes()
    {
        var definition = new ProbeDefinition();

        Should.Throw<ArgumentNullException>(() => definition.PaymentColumn.WithValueLabelKeyPrefix(null!));
        Should.Throw<ArgumentException>(() => definition.PaymentColumn.WithValueLabelKeyPrefix(string.Empty));
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

        public PaymentMethod Payment { get; set; } = PaymentMethod.IBAN;

        public PaymentMethod? OptionalPayment { get; set; } = PaymentMethod.IBAN;
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

        public PaymentMethod Payment { get; set; } = PaymentMethod.IBAN;

        public PaymentMethod? OptionalPayment { get; set; } = PaymentMethod.IBAN;
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

            this.PaymentColumn = this.Column(d => d.Payment);
            this.Column(d => d.OptionalPayment);
        }

        public TableViewColumnDefinition<Machine, MachineDto> NameColumn { get; }

        public TableViewColumnDefinition<Machine, MachineDto> PaymentColumn { get; }

        public TableViewColumnDefinition<Machine, MachineDto> PriceColumn { get; }

        public TableViewColumnMetadata MetadataFor(string columnName)
        {
            return this.Metadata.Single(m => m.Name == columnName);
        }
    }
}
