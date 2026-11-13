namespace Csag.Blueprint.Infrastructure.UnitTests.Countries;

using Csag.Blueprint.Infrastructure.Countries;

/// <summary>
/// Unit tests for <see cref="NagerCountryCatalog"/>.
/// </summary>
public sealed class NagerCountryCatalogTests
{
    private readonly NagerCountryCatalog catalog = new();

    [Fact]
    public void Codes_HasTheExpectedNumberOfCodes()
    {
        this.catalog.Codes.Count.ShouldBe(250);
    }

    [Fact]
    public void Codes_IsSortedAndUnique()
    {
        var codes = this.catalog.Codes;

        codes.ShouldBe(codes.Order(StringComparer.Ordinal).Distinct());
    }

    [Fact]
    public void Codes_ContainsOnlyTwoUppercaseLetters()
    {
        this.catalog.Codes.ShouldAllBe(c => c.Length == 2 && char.IsAsciiLetterUpper(c[0]) && char.IsAsciiLetterUpper(c[1]));
    }

    [Theory]
    [InlineData("CH")]
    [InlineData("DE")]
    [InlineData("GB")]
    [InlineData("FR")]
    [InlineData("SS")]
    [InlineData("AX")]
    public void Contains_KnownCode_ReturnsTrue(string code)
    {
        this.catalog.Contains(code).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ch")]
    [InlineData("UK")]
    [InlineData("EU")]
    [InlineData("ZZ")]
    [InlineData("YU")]
    [InlineData("CHE")]
    [InlineData(" CH")]
    [InlineData("6570")]
    public void Contains_UnknownValue_ReturnsFalse(string? code)
    {
        this.catalog.Contains(code).ShouldBeFalse();
    }
}
