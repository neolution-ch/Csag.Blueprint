namespace Csag.Blueprint.Infrastructure.UnitTests.Localization;

using System.Globalization;
using Csag.Blueprint.Infrastructure.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit tests for <see cref="BlueprintDbStringLocalizer"/> (and briefly its factory): snapshot lookup
/// for the current UI culture, the English code-default fallback with the <c>ResourceNotFound</c>
/// flag, and the dual placeholder formats — named placeholders from an anonymous object vs
/// positional <c>string.Format</c> arguments.
/// </summary>
public sealed class BlueprintDbStringLocalizerTests
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["Greeting.Hello"] = "Hello",
        ["Validation.Required"] = "The field is required",
    };

    [Fact]
    public void Indexer_KnownKey_ReturnsSnapshotValueForCurrentUiCulture()
    {
        // Arrange
        var provider = CreateProvider(new Dictionary<string, string> { ["Greeting.Hello"] = "Grüezi" });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Greeting.Hello"];

        // Assert
        result.Value.ShouldBe("Grüezi");
        result.ResourceNotFound.ShouldBeFalse();
        result.Name.ShouldBe("Greeting.Hello");
        provider.Verify(p => p.GetTranslations(CultureInfo.CurrentUICulture.Name), Times.Once);
    }

    [Fact]
    public void Indexer_MissingKey_FallsBackToEnglishDefaultAndFlagsNotFound()
    {
        // Arrange — the snapshot has no entry for the key, but the code defaults do.
        var provider = CreateProvider([]);
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Validation.Required"];

        // Assert
        result.Value.ShouldBe("The field is required");
        result.ResourceNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Indexer_KeyUnknownEverywhere_ReturnsTheKeyItself()
    {
        // Arrange
        var provider = CreateProvider([]);
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Unknown.Key"];

        // Assert
        result.Value.ShouldBe("Unknown.Key");
        result.ResourceNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Indexer_NamedPlaceholders_ReplacedFromAnonymousObject()
    {
        // Arrange
        var provider = CreateProvider(new Dictionary<string, string>
        {
            ["Greeting.Hello"] = "Hello {Name}, you have {Count} items",
        });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Greeting.Hello", new { Name = "Alice", Count = 3 }];

        // Assert
        result.Value.ShouldBe("Hello Alice, you have 3 items");
        result.ResourceNotFound.ShouldBeFalse();
    }

    [Fact]
    public void Indexer_NamedPlaceholders_MatchCaseInsensitivelyAndLeaveUnknownOnesVerbatim()
    {
        // Arrange — {NAME} matches the Name property; {Other} has no matching property.
        var provider = CreateProvider(new Dictionary<string, string>
        {
            ["Greeting.Hello"] = "Hello {NAME}, {Other}",
        });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Greeting.Hello", new { Name = "Alice" }];

        // Assert — unknown placeholders stay literal instead of throwing or vanishing.
        result.Value.ShouldBe("Hello Alice, {Other}");
    }

    [Fact]
    public void Indexer_StringArgument_UsesPositionalFormatting()
    {
        // Arrange — a string first argument selects positional string.Format, not named replacement.
        var provider = CreateProvider(new Dictionary<string, string>
        {
            ["Greeting.Hello"] = "{0} has {1} items",
        });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Greeting.Hello", "Alice", 3];

        // Assert
        result.Value.ShouldBe("Alice has 3 items");
    }

    [Fact]
    public void Indexer_ValueTypeArgument_UsesPositionalFormatting()
    {
        // Arrange — decimals (like all primitives, dates, and Guids) are positional arguments even
        // though they are not strings.
        var provider = CreateProvider(new Dictionary<string, string>
        {
            ["Greeting.Hello"] = "Price: {0}",
        });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer["Greeting.Hello", 9.5m];

        // Assert
        result.Value.ShouldBe(string.Format(CultureInfo.CurrentCulture, "Price: {0}", 9.5m));
    }

    [Fact]
    public void GetAllStrings_ReturnsEveryEntryOfTheSnapshot()
    {
        // Arrange
        var provider = CreateProvider(new Dictionary<string, string>
        {
            ["Greeting.Hello"] = "Grüezi",
            ["Validation.Required"] = "Das Feld ist erforderlich",
        });
        var localizer = CreateLocalizer(provider);

        // Act
        var result = localizer.GetAllStrings(includeParentCultures: true).ToList();

        // Assert
        result.Select(s => (s.Name, s.Value, s.ResourceNotFound)).ShouldBe(
            [
                ("Greeting.Hello", "Grüezi", false),
                ("Validation.Required", "Das Feld ist erforderlich", false),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Factory_BothCreateOverloads_ReturnWorkingLocalizers()
    {
        // Arrange — translations are global, so the resource type/name arguments are irrelevant and
        // both overloads must produce equivalent localizers.
        var provider = CreateProvider(new Dictionary<string, string> { ["Greeting.Hello"] = "Grüezi" });
        var factory = new BlueprintDbStringLocalizerFactory(provider.Object, NullLoggerFactory.Instance, Defaults);

        // Act
        var byType = factory.Create(typeof(BlueprintDbStringLocalizerTests));
        var byName = factory.Create("AnyBaseName", "AnyLocation");

        // Assert
        byType.ShouldBeOfType<BlueprintDbStringLocalizer>();
        byName.ShouldBeOfType<BlueprintDbStringLocalizer>();
        byType["Greeting.Hello"].Value.ShouldBe("Grüezi");
        byName["Greeting.Hello"].Value.ShouldBe("Grüezi");
    }

    private static Mock<ITranslationProvider> CreateProvider(Dictionary<string, string> translations)
    {
        var provider = new Mock<ITranslationProvider>();
        provider
            .Setup(p => p.GetTranslations(It.IsAny<string>()))
            .Returns(new TranslationSnapshot { Translations = translations });
        return provider;
    }

    private static BlueprintDbStringLocalizer CreateLocalizer(Mock<ITranslationProvider> provider)
    {
        return new BlueprintDbStringLocalizer(provider.Object, NullLogger<BlueprintDbStringLocalizer>.Instance, Defaults);
    }
}
