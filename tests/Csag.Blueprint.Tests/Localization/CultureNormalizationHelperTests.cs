namespace Csag.Blueprint.Tests.Localization;

using System.Globalization;
using Csag.Blueprint.Web.Services;

public sealed class CultureNormalizationHelperTests
{
    private static readonly List<CultureInfo> SupportedCultures =
        [new CultureInfo("en-GB"), new CultureInfo("de-CH"), new CultureInfo("fr-CH")];

    private static readonly List<string> SupportedLanguages = ["en-GB", "de-CH", "fr-CH"];

    [Theory]
    [InlineData("de-CH", "de-CH")]
    [InlineData("DE-ch", "de-CH")]
    [InlineData("en-GB", "en-GB")]
    public void FindMatchingCulture_MatchesExactTagCaseInsensitively(string requested, string expected)
    {
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBe(expected);
    }

    [Theory]
    [InlineData("de", "de-CH")]
    [InlineData("fr", "fr-CH")]
    public void FindMatchingCulture_ResolvesBareLanguageToItsRegionalVariant(string requested, string expected)
    {
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void FindMatchingCulture_DoesNotCrossRegionsForFullTags(string requested)
    {
        // The behaviour change: a full IETF tag must match exactly rather than silently resolving to
        // another region of the same language.
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBeNull();
    }

    [Theory]
    [InlineData("es")]
    [InlineData("not-a-culture-tag")]
    [InlineData("")]
    [InlineData(null)]
    public void FindMatchingCulture_ReturnsNullForUnsupportedOrInvalidInput(string? requested)
    {
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBeNull();
    }

    [Theory]
    [InlineData("de-CH", "de-CH")]
    [InlineData("de", "de-CH")]
    public void FindMatchingLanguage_MatchesExactTagsAndBareLanguages(string requested, string expected)
    {
        CultureNormalizationHelper.FindMatchingLanguage(requested, SupportedLanguages).ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void FindMatchingLanguage_DoesNotCrossRegionsForFullTags(string requested)
    {
        CultureNormalizationHelper.FindMatchingLanguage(requested, SupportedLanguages).ShouldBeNull();
    }

    [Fact]
    public void IsSupportedCulture_TracksFindMatchingCulture()
    {
        CultureNormalizationHelper.IsSupportedCulture("de", SupportedCultures).ShouldBeTrue();
        CultureNormalizationHelper.IsSupportedCulture("fr-FR", SupportedCultures).ShouldBeFalse();
    }

    [Fact]
    public void IsSupportedLanguage_TracksFindMatchingLanguage()
    {
        CultureNormalizationHelper.IsSupportedLanguage("de", SupportedLanguages).ShouldBeTrue();
        CultureNormalizationHelper.IsSupportedLanguage("fr-FR", SupportedLanguages).ShouldBeFalse();
    }
}
