namespace Csag.Blueprint.Web.UnitTests.Services;

using System.Globalization;
using Csag.Blueprint.Web.Services;

/// <summary>
/// Unit tests for <see cref="CultureNormalizationHelper"/> — the case-insensitive culture matching
/// shared by the request-culture provider and the profile validation. Table-driven over the matching
/// order: exact (case-insensitive) match first, then the two-letter language fallback, then null for
/// anything unsupported or unparseable.
/// </summary>
public sealed class CultureNormalizationHelperTests
{
    private static readonly List<CultureInfo> SupportedCultures = new()
    {
        new CultureInfo("de-CH"),
        new CultureInfo("en-US"),
    };

    private static readonly List<string> SupportedLanguages = new() { "de-CH", "en" };

    [Theory]
    [InlineData("de-CH", "de-CH")] // exact
    [InlineData("DE-ch", "de-CH")] // exact, case-insensitive
    [InlineData(" de-CH ", "de-CH")] // trimmed before matching
    [InlineData("en-US", "en-US")]
    [InlineData("de", "de-CH")] // language-only fallback
    [InlineData("de-AT", "de-CH")] // different region falls back to the language match
    [InlineData("EN", "en-US")]
    [InlineData("fr", null)] // valid culture, but unsupported
    [InlineData("fr-FR", null)]
    [InlineData("!!invalid!!", null)] // unparseable culture name
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void FindMatchingCulture_ReturnsExpected(string? requested, string? expected)
    {
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBe(expected);
    }

    [Fact]
    public void FindMatchingCulture_EmptySupportedList_ReturnsNull()
    {
        CultureNormalizationHelper.FindMatchingCulture("de-CH", new List<CultureInfo>()).ShouldBeNull();
    }

    [Fact]
    public void FindMatchingCulture_NullSupportedList_ReturnsNull()
    {
        CultureNormalizationHelper.FindMatchingCulture("de-CH", null!).ShouldBeNull();
    }

    [Theory]
    [InlineData("de-CH", "de-CH")] // exact
    [InlineData("DE-CH", "de-CH")] // exact, case-insensitive
    [InlineData("en", "en")]
    [InlineData("de", "de-CH")] // language-only fallback against a region-qualified entry
    [InlineData("en-GB", "en")] // region-qualified request falls back to the bare language entry
    [InlineData("fr", null)]
    [InlineData("!!invalid!!", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FindMatchingLanguage_ReturnsExpected(string? requested, string? expected)
    {
        CultureNormalizationHelper.FindMatchingLanguage(requested, SupportedLanguages).ShouldBe(expected);
    }

    [Fact]
    public void FindMatchingLanguage_UnparseableSupportedEntry_IsSkipped()
    {
        // A bad entry in configuration must not break matching for the entries after it.
        var supported = new List<string> { "!!invalid!!", "de-CH" };

        CultureNormalizationHelper.FindMatchingLanguage("de", supported).ShouldBe("de-CH");
    }

    [Fact]
    public void FindMatchingLanguage_EmptySupportedList_ReturnsNull()
    {
        CultureNormalizationHelper.FindMatchingLanguage("de", new List<string>()).ShouldBeNull();
    }

    [Theory]
    [InlineData("de-CH", true)]
    [InlineData("de", true)] // supported via the language fallback
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupportedCulture_ReturnsExpected(string? requested, bool expected)
    {
        CultureNormalizationHelper.IsSupportedCulture(requested, SupportedCultures).ShouldBe(expected);
    }

    [Theory]
    [InlineData("en-GB", true)]
    [InlineData("de-CH", true)]
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupportedLanguage_ReturnsExpected(string? requested, bool expected)
    {
        CultureNormalizationHelper.IsSupportedLanguage(requested, SupportedLanguages).ShouldBe(expected);
    }
}
