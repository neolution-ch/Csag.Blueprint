namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="StringLocalizerTableViewMetadataLocalizer"/> and
/// <see cref="NoOpTableViewMetadataLocalizer"/>.
/// </summary>
public sealed class TableViewMetadataLocalizerTests
{
    private const string KindKey = "Vehicles.TableViewColumns.Kind";
    private const string KayakKey = "Enums.TestVehicleKind.Kayak";

    [Fact]
    public void Localize_ColumnWithDisplayNameKey_ResolvesTranslatedDisplayName()
    {
        // Arrange
        var localizer = CreateLocalizer((KindKey, "Art"));
        var column = CreateColumn("Kind", displayNameKey: KindKey);

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result.Count.ShouldBe(1);
        result[0].DisplayName.ShouldBe("Art");
        result[0].Name.ShouldBe("Kind");
        result[0].DataType.ShouldBe("string");
        result[0].IsSortable.ShouldBeTrue();
    }

    [Fact]
    public void Localize_ColumnWithDescriptionKey_ResolvesTranslatedDescription()
    {
        // Arrange
        var localizer = CreateLocalizer((KindKey, "Art des Fahrzeugs"));
        var column = CreateColumn("Kind", descriptionKey: KindKey);

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result[0].Description.ShouldBe("Art des Fahrzeugs");
        result[0].DisplayName.ShouldBe("Kind");
    }

    [Fact]
    public void Localize_DoesNotMutateDefinitionOwnedMetadata()
    {
        // The definition owns the metadata instances; mutating them at request time would leak one
        // request's culture into another if definitions are ever registered with a longer lifetime.
        // Arrange
        var localizer = CreateLocalizer((KindKey, "Art"));
        var column = CreateColumn("Kind", displayNameKey: KindKey);

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result[0].ShouldNotBeSameAs(column);
        column.DisplayName.ShouldBe("Kind");
        result[0].DisplayName.ShouldBe("Art");
    }

    [Fact]
    public void Localize_KeyNotFound_KeepsDefaultDisplayName()
    {
        // Arrange — a key without a translation must never leak the raw dot-path to the user.
        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name, resourceNotFound: true));
        var localizer = new StringLocalizerTableViewMetadataLocalizer(
            localizerMock.Object,
            NullLogger<StringLocalizerTableViewMetadataLocalizer>.Instance);
        var column = CreateColumn("Kind", displayNameKey: "Some.Missing.Key");

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result[0].DisplayName.ShouldBe("Kind");
    }

    [Fact]
    public void Localize_ColumnWithoutKeys_ReturnsSameInstanceUnchanged()
    {
        // Arrange
        var localizerMock = new Mock<IStringLocalizer>(MockBehavior.Strict);
        var localizer = new StringLocalizerTableViewMetadataLocalizer(
            localizerMock.Object,
            NullLogger<StringLocalizerTableViewMetadataLocalizer>.Instance);
        var column = CreateColumn("Kind");

        // Act
        var result = localizer.Localize([column]);

        // Assert — no keys means no clone and no localizer access at all (strict mock verifies).
        result[0].ShouldBeSameAs(column);
        result[0].DisplayName.ShouldBe("Kind");
    }

    [Fact]
    public void Localize_PreservesColumnOrder()
    {
        // Arrange
        var localizer = CreateLocalizer((KindKey, "Art"));
        var first = CreateColumn("Kind", displayNameKey: KindKey);
        var second = CreateColumn("CreatedAt");

        // Act
        var result = localizer.Localize([first, second]);

        // Assert
        result.Select(c => c.Name).ShouldBe(["Kind", "CreatedAt"]);
    }

    [Fact]
    public void NoOpLocalizer_ReturnsColumnsUnchanged()
    {
        // Arrange
        var localizer = new NoOpTableViewMetadataLocalizer();
        var column = CreateColumn("Kind", displayNameKey: KindKey);
        var columns = new List<TableViewColumnMetadata> { column };

        // Act
        var result = localizer.Localize(columns);

        // Assert
        result.ShouldBeSameAs(columns);
        result[0].DisplayName.ShouldBe("Kind");
    }

    [Fact]
    public void Localize_EnumColumn_ResolvesTranslatedConventionLabels()
    {
        // Arrange — the Kind column derives a convention key per enum member, and only the
        // Kayak member's key has a translation.
        var localizer = CreateLocalizer((KayakKey, "Kajak"));
        var kind = new TestVehicleTableViewDefinition().Metadata.Single(c => c.Name == "Kind");

        // Act
        var result = localizer.Localize([kind]);

        // Assert
        var allowedValues = result[0].AllowedValues.ShouldNotBeNull();
        allowedValues.Select(v => v.Value).ShouldBe(["None", "Bicycle", "Scooter", "Kayak"]);
        allowedValues.Select(v => v.Label).ShouldBe(["None", "Bicycle", "Scooter", "Kajak"]);
    }

    [Fact]
    public void Localize_ConventionLabelKeyNotFound_KeepsHumanizedLabelWithoutWarning()
    {
        // Arrange — convention keys are optional: most enum labels are never translated.
        var logger = new RecordingLogger();
        var localizer = CreateLocalizer(logger);
        var column = CreateColumn(
            "Payment",
            allowedValues: [new("CreditCard", "Credit card") { LabelKey = "Enums.PaymentMethod.CreditCard", IsLabelKeyOptional = true }]);

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result[0].AllowedValues.ShouldNotBeNull().Single().Label.ShouldBe("Credit card");
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Localize_ExplicitLabelKeyNotFound_KeepsLabelAndLogsWarning()
    {
        // Arrange — a key the definition author set is expected to exist.
        var logger = new RecordingLogger();
        var localizer = CreateLocalizer(logger);
        var column = CreateColumn(
            "Size",
            allowedValues: [new("S", "Small") { LabelKey = "Sizes.Small" }]);

        // Act
        var result = localizer.Localize([column]);

        // Assert
        result[0].AllowedValues.ShouldNotBeNull().Single().Label.ShouldBe("Small");
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("Sizes.Small");
        entry.Message.ShouldContain("Size");
    }

    [Fact]
    public void Localize_ResolvesKeyedLabelsAndKeepsUnkeyedOnes()
    {
        // Arrange
        var localizer = CreateLocalizer(("Sizes.Large", "Groß"));
        var column = CreateColumn(
            "Size",
            allowedValues: [new("S", "Small"), new("L", "Large") { LabelKey = "Sizes.Large" }]);

        // Act
        var result = localizer.Localize([column]);

        // Assert — only the label changes; the value stays what filters match on.
        result[0].AllowedValues.ShouldBe([new("S", "Small"), new("L", "Groß") { LabelKey = "Sizes.Large" }]);
        result[0].DisplayName.ShouldBe("Size");
    }

    [Fact]
    public void Localize_DoesNotMutateDefinitionOwnedAllowedValues()
    {
        // Arrange
        var localizer = CreateLocalizer((KayakKey, "Kajak"));
        var definition = new TestVehicleTableViewDefinition();
        var kind = definition.Metadata.Single(c => c.Name == "Kind");
        var ownedValues = kind.AllowedValues.ShouldNotBeNull();

        // Act
        var result = localizer.Localize([kind]);

        // Assert — the request gets its own column and list; the definition's stay untouched.
        result[0].ShouldNotBeSameAs(kind);
        result[0].AllowedValues.ShouldNotBeSameAs(ownedValues);
        kind.AllowedValues.ShouldBeSameAs(ownedValues);
        ownedValues.Single(v => v.Value == "Kayak").Label.ShouldBe("Kayak");
        definition.Metadata.Single(c => c.Name == "Kind").AllowedValues.ShouldNotBeNull()
            .Single(v => v.Value == "Kayak").Label.ShouldBe("Kayak");
    }

    [Fact]
    public void Localize_AllowedValuesWithoutKeys_ReturnsSameInstanceUnchanged()
    {
        // Arrange
        var localizerMock = new Mock<IStringLocalizer>(MockBehavior.Strict);
        var localizer = new StringLocalizerTableViewMetadataLocalizer(
            localizerMock.Object,
            NullLogger<StringLocalizerTableViewMetadataLocalizer>.Instance);
        var column = CreateColumn("Size", allowedValues: [new("S", "Small")]);

        // Act
        var result = localizer.Localize([column]);

        // Assert — no keys anywhere means no clone and no localizer access (strict mock verifies).
        result[0].ShouldBeSameAs(column);
    }

    private static StringLocalizerTableViewMetadataLocalizer CreateLocalizer(params (string Key, string Value)[] translations)
    {
        return CreateLocalizer(NullLogger<StringLocalizerTableViewMetadataLocalizer>.Instance, translations);
    }

    private static StringLocalizerTableViewMetadataLocalizer CreateLocalizer(
        ILogger<StringLocalizerTableViewMetadataLocalizer> logger,
        params (string Key, string Value)[] translations)
    {
        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name, resourceNotFound: true));

        foreach (var (key, value) in translations)
        {
            localizerMock
                .Setup(l => l[key])
                .Returns(new LocalizedString(key, value, resourceNotFound: false));
        }

        return new StringLocalizerTableViewMetadataLocalizer(localizerMock.Object, logger);
    }

    private static TableViewColumnMetadata CreateColumn(
        string name,
        string? displayNameKey = null,
        string? descriptionKey = null,
        IReadOnlyList<TableViewAllowedValue>? allowedValues = null)
    {
        return new TableViewColumnMetadata
        {
            Name = name,
            DisplayName = name,
            DisplayNameKey = displayNameKey,
            DataType = "string",
            Description = name,
            DescriptionKey = descriptionKey,
            IsSortable = true,
            AllowedValues = allowedValues,
        };
    }

    private sealed class RecordingLogger : ILogger<StringLocalizerTableViewMetadataLocalizer>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
