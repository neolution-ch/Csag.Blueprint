namespace Csag.Blueprint.Web.UnitTests.Services;

using System.Globalization;
using Csag.Blueprint.Web.Services;

/// <summary>
/// Unit tests for <see cref="CultureNormalizationHelper"/> — the case-insensitive culture matching
/// shared by the request-culture provider and the profile validation. Table-driven over the matching
/// order: exact (case-insensitive) match first, then the two-letter language fallback, then null for
/// anything unsupported or unparseable. The language fallback narrows but never swaps regions, so
/// "de" resolves to "de-CH" and "en-GB" resolves to a bare "en", while "de-AT" resolves to neither.
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
    [InlineData("de-AT", null)] // a different region is a swap, not a narrowing — no match
    [InlineData("EN", "en-US")]
    [InlineData("fr", null)] // valid culture, but unsupported
    [InlineData("fr-FR", null)]
    [InlineData("en-GB", null)] // en-US is region-qualified too, so this would be a swap
    [InlineData("!!invalid!!", null)] // unparseable culture name
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void FindMatchingCulture_ReturnsExpected(string? requested, string? expected)
    {
        CultureNormalizationHelper.FindMatchingCulture(requested, SupportedCultures).ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr-FR", "fr-CH")] // same language, both region-qualified, different region
    [InlineData("de-DE", "de-CH")]
    [InlineData("en-US", "en-GB")]
    public void FindMatchingCulture_DoesNotCrossRegions(string requested, string supported)
    {
        // The no-region-swap rule: a full IETF tag must not silently resolve to another region of
        // the same language. Narrowing to a bare language entry stays allowed (covered above).
        CultureNormalizationHelper.FindMatchingCulture(requested, new List<CultureInfo> { new(supported) }).ShouldBeNull();
    }

    [Theory]
    [InlineData("fr-FR", "fr-CH")]
    [InlineData("en-US", "en-GB")]
    public void FindMatchingLanguage_DoesNotCrossRegions(string requested, string supported)
    {
        CultureNormalizationHelper.FindMatchingLanguage(requested, new List<string> { supported }).ShouldBeNull();
    }

    [Fact]
    public void FindMatchingCulture_BareRequest_ResolvesToTheRegionalVariant()
    {
        var supported = new List<CultureInfo> { new("en-GB"), new("de-CH"), new("fr-CH") };

        CultureNormalizationHelper.FindMatchingCulture("fr", supported).ShouldBe("fr-CH");
        CultureNormalizationHelper.FindMatchingCulture("de", supported).ShouldBe("de-CH");
    }

    [Theory]
    [InlineData("en-US-u-nu-latn", "en-US")] // same region, extension only on the request
    [InlineData("zh-Hans-CN", "zh-Hans")] // same script, region only on the request
    [InlineData("de-Latn-CH", "de-CH")] // same region, script only on the request
    public void FindMatchingCulture_NarrowsWhenTheCandidateLeavesSubtagsUnspecified(string requested, string supported)
    {
        // The no-region-swap rule compares subtags, not whole tags: a candidate that does not specify a
        // region or script is a narrowing target, not a swap. Comparing full tags rejected all of these.
        CultureNormalizationHelper.FindMatchingCulture(requested, new List<CultureInfo> { new(supported) })
            .ShouldBe(supported);
    }

    [Theory]
    [InlineData("zh-Hant-CN", "zh-Hans-CN")] // identical region, different script
    [InlineData("zh-Hans", "zh-Hant")]
    public void FindMatchingCulture_DoesNotCrossScripts(string requested, string supported)
    {
        // Serving Simplified where Traditional was asked for is the same class of mistake as serving
        // another region, and the regions here are identical, so a region-only check would allow it.
        CultureNormalizationHelper.FindMatchingCulture(requested, new List<CultureInfo> { new(supported) })
            .ShouldBeNull();
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
