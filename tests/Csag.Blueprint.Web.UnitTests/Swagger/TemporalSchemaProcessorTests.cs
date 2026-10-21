namespace Csag.Blueprint.Web.UnitTests.Swagger;

using Csag.Blueprint.Web.Swagger;
using NJsonSchema;
using NJsonSchema.Generation;

/// <summary>
/// Unit tests for <see cref="TemporalSchemaProcessor"/>: the generated schemas describe what
/// System.Text.Json writes with the Blueprint conventions.
/// </summary>
public sealed class TemporalSchemaProcessorTests
{
    private const string LocalDateTimePattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$";

    private static readonly JsonSchema Schema = CreateSchema();

    [Theory]
    [InlineData("startLocal")]
    [InlineData("endLocal")]
    public void Process_DateTimeProperty_IsLocalDateTimeWithPattern(string propertyName)
    {
        var property = Schema.Properties[propertyName];

        property.Format.ShouldBe("local-date-time");
        property.Pattern.ShouldBe(LocalDateTimePattern);
        property.Type.HasFlag(JsonObjectType.String).ShouldBeTrue();
    }

    [Fact]
    public void Process_NullableDateTimeProperty_StaysNullable()
    {
        Schema.Properties["endLocal"].IsNullable(SchemaType.OpenApi3).ShouldBeTrue();
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public void Process_DateTimeOffsetProperty_KeepsDateTimeFormat(string propertyName)
    {
        var property = Schema.Properties[propertyName];

        property.Format.ShouldBe(JsonFormatStrings.DateTime);
        property.Pattern.ShouldBeNull();
    }

    [Theory]
    [InlineData("rentalLength")]
    [InlineData("optionalLength")]
    public void Process_TimeSpanProperty_DescribesConstantFormatInsteadOfIsoDuration(string propertyName)
    {
        var property = Schema.Properties[propertyName];

        property.Format.ShouldBeNull();
        property.Pattern.ShouldBe(@"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$");
    }

    [Theory]
    [InlineData("02:30:00", true)]
    [InlineData("1.02:30:00", true)]
    [InlineData("-00:00:01.5", true)]
    [InlineData("PT2H30M", false)]
    public void TimeSpanPattern_MatchesWhatSystemTextJsonWrites(string value, bool expected)
    {
        System.Text.RegularExpressions.Regex.IsMatch(value, Schema.Properties["rentalLength"].Pattern!, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBe(expected);
    }

    [Fact]
    public void Process_DateOnlyProperty_KeepsDateFormat()
    {
        Schema.Properties["purchaseDate"].Format.ShouldBe(JsonFormatStrings.Date);
    }

    private static JsonSchema CreateSchema()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings { SchemaType = SchemaType.OpenApi3 };
        settings.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        settings.SchemaProcessors.Add(new TemporalSchemaProcessor());
        return JsonSchema.FromType<Contract>(settings);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Only inspected by the schema generator.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S1144:Unused private types or members should be removed", Justification = "The properties are read by the schema generator through reflection.")]
    private sealed class Contract
    {
        public DateTime StartLocal { get; set; }

        public DateTime? EndLocal { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public TimeSpan RentalLength { get; set; }

        public TimeSpan? OptionalLength { get; set; }

        public DateOnly PurchaseDate { get; set; }
    }
}
