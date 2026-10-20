namespace Csag.Blueprint.Application.UnitTests.TableView;

using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;

/// <summary>
/// Pins down the JSON contract for <see cref="TableViewAllowedValue"/>: clients receive
/// <c>{ "value": "...", "label": "..." }</c> and never the translation key or its optional flag.
/// Uses the production <see cref="BlueprintJsonOptions.Default"/> instance so this test fails
/// (rather than silently drifting) if someone changes the naming policy in shared config.
/// </summary>
public sealed class TableViewAllowedValueJsonTests
{
    private static readonly JsonSerializerOptions Options = BlueprintJsonOptions.Default;

    [Fact]
    public void Serialize_WritesOnlyValueAndLabel()
    {
        // Arrange
        var allowedValue = new TableViewAllowedValue("CreditCard", "Credit card")
        {
            LabelKey = "Enums.PaymentMethod.CreditCard",
            IsLabelKeyOptional = true,
        };

        // Act
        var json = JsonSerializer.Serialize(allowedValue, Options);

        // Assert
        json.ShouldBe("""{"value":"CreditCard","label":"Credit card"}""");
    }

    [Fact]
    public void Serialize_ColumnMetadata_WritesAllowedValuesAsValueLabelObjects()
    {
        // Arrange
        var column = new TableViewColumnMetadata
        {
            Name = "Payment",
            DataType = "enum",
            AllowedValues =
            [
                new("CreditCard", "Credit card") { LabelKey = "Enums.PaymentMethod.CreditCard" },
                new("BankTransfer", "Bank transfer"),
            ],
        };

        // Act
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(column, Options));

        // Assert
        var allowedValues = document.RootElement.GetProperty("allowedValues").EnumerateArray().ToList();
        allowedValues.Count.ShouldBe(2);
        allowedValues[0].EnumerateObject().Select(p => p.Name).ShouldBe(["value", "label"]);
        allowedValues[0].GetProperty("value").GetString().ShouldBe("CreditCard");
        allowedValues[0].GetProperty("label").GetString().ShouldBe("Credit card");
    }

    [Fact]
    public void Deserialize_ReadsValueAndLabel()
    {
        // Act
        var result = JsonSerializer.Deserialize<TableViewAllowedValue>("""{"value":"CreditCard","label":"Credit card"}""", Options);

        // Assert
        result.ShouldBe(new TableViewAllowedValue("CreditCard", "Credit card"));
    }
}
