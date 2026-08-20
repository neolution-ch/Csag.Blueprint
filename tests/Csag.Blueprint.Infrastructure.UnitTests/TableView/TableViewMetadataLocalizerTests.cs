namespace Csag.Blueprint.Infrastructure.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Infrastructure.TableView;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="StringLocalizerTableViewMetadataLocalizer"/> and
/// <see cref="NoOpTableViewMetadataLocalizer"/>.
/// </summary>
public sealed class TableViewMetadataLocalizerTests
{
    private const string KindKey = "Vehicles.TableViewColumns.Kind";

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

    private static StringLocalizerTableViewMetadataLocalizer CreateLocalizer(params (string Key, string Value)[] translations)
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

        return new StringLocalizerTableViewMetadataLocalizer(
            localizerMock.Object,
            NullLogger<StringLocalizerTableViewMetadataLocalizer>.Instance);
    }

    private static TableViewColumnMetadata CreateColumn(
        string name,
        string? displayNameKey = null,
        string? descriptionKey = null)
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
        };
    }
}
