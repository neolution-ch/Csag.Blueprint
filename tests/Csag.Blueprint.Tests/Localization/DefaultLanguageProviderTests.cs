namespace Csag.Blueprint.Tests.Localization;

using System.Globalization;
using Csag.Blueprint.Infrastructure.Localization;

public sealed class DefaultLanguageProviderTests
{
    [Fact]
    public void CurrentLanguageCode_ReflectsAmbientUiCulture()
    {
        WithUiCulture(new CultureInfo("de-CH"), () =>
        {
            var provider = new DefaultLanguageProvider("en-GB");
            provider.CurrentLanguageCode.ShouldBe("de-CH");
        });
    }

    [Fact]
    public void CurrentLanguageCode_FollowsCultureChangesBetweenReads()
    {
        var provider = new DefaultLanguageProvider("en-GB");

        WithUiCulture(new CultureInfo("fr-CH"), () => provider.CurrentLanguageCode.ShouldBe("fr-CH"));
        WithUiCulture(new CultureInfo("it-CH"), () => provider.CurrentLanguageCode.ShouldBe("it-CH"));
    }

    [Fact]
    public void CurrentLanguageCode_FallsBackWhenCultureIsInvariant()
    {
        WithUiCulture(CultureInfo.InvariantCulture, () =>
        {
            var provider = new DefaultLanguageProvider("en-GB");
            provider.CurrentLanguageCode.ShouldBe("en-GB");
        });
    }

    [Fact]
    public void FallbackLanguageCode_DefaultsToEnglish()
    {
        new DefaultLanguageProvider().FallbackLanguageCode.ShouldBe("en");
    }

    [Fact]
    public void Constructor_RejectsBlankFallback()
    {
        Should.Throw<ArgumentException>(() => new DefaultLanguageProvider("  "));
    }

    private static void WithUiCulture(CultureInfo culture, Action assertion)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            assertion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
