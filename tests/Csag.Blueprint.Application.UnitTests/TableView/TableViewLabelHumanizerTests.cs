namespace Csag.Blueprint.Application.UnitTests.TableView;

using System.Reflection;
using Csag.Blueprint.Application.TableView;

/// <summary>
/// Unit tests for <c>TableViewLabelHumanizer.Humanize</c>, which turns enum member names into the
/// labels served for enum columns' allowed values. The type is internal and the package declares
/// no <c>InternalsVisibleTo</c>, so it is reached via reflection on the Application assembly.
/// </summary>
public sealed class TableViewLabelHumanizerTests
{
    private static readonly MethodInfo HumanizeMethod = typeof(TableViewAllowedValue).Assembly
        .GetType("Csag.Blueprint.Application.TableView.TableViewLabelHumanizer", throwOnError: true)!
        .GetMethod("Humanize", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("Kayak", "Kayak")]
    [InlineData("CreditCard", "Credit card")]
    [InlineData("BankTransfer", "Bank transfer")]
    [InlineData("FourSeater", "Four seater")]
    [InlineData("TwoSeater", "Two seater")]
    public void Humanize_PascalCase_SplitsIntoSentenceCase(string name, string expected)
    {
        Humanize(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("IBAN", "IBAN")]
    [InlineData("SMSCode", "SMS code")]
    [InlineData("PayByIBAN", "Pay by IBAN")]
    [InlineData("ABTest", "AB test")]
    [InlineData("PlanA", "Plan A")]
    public void Humanize_Capitals_KeepsAcronymsAndSingleLettersIntact(string name, string expected)
    {
        Humanize(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Level2", "Level 2")]
    [InlineData("Top10Items", "Top 10 items")]
    [InlineData("Base64Url", "Base 64 url")]
    [InlineData("SMS2FA", "SMS 2 FA")]
    public void Humanize_Digits_FormTheirOwnWord(string name, string expected)
    {
        Humanize(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("creditCard", "Credit card")]
    [InlineData("Credit_Card", "Credit card")]
    [InlineData("_Leading__Separators_", "Leading separators")]
    [InlineData("x", "X")]
    public void Humanize_OtherIdentifierStyles_CapitalizesAndSplitsAtSeparators(string name, string expected)
    {
        Humanize(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("__")]
    public void Humanize_NoLettersOrDigits_ReturnsTheNameUnchanged(string name)
    {
        Humanize(name).ShouldBe(name);
    }

    private static string Humanize(string name)
    {
        return (string)HumanizeMethod.Invoke(null, [name])!;
    }
}
