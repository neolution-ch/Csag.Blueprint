namespace Csag.Blueprint.Web.UnitTests.Swagger;

using System.Text.Json;
using System.Text.RegularExpressions;
using Csag.Blueprint.Application.Json;
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

    private const string LocalTimePattern = @"^\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

    private static readonly JsonSchema Schema = CreateSchema();

    [Theory]
    [InlineData("startLocal")]
    [InlineData("endLocal")]
    public void Process_DateTimeProperty_IsDateTimeLocalWithPattern(string propertyName)
    {
        var property = Schema.Properties[propertyName];

        property.Format.ShouldBe("date-time-local");
        property.Pattern.ShouldBe(LocalDateTimePattern);
        property.Type.HasFlag(JsonObjectType.String).ShouldBeTrue();
    }

    [Fact]
    public void Process_NullableDateTimeProperty_StaysNullable()
    {
        Schema.Properties["endLocal"].IsNullable(SchemaType.OpenApi3).ShouldBeTrue();
    }

    [Theory]
    [InlineData("opensAt")]
    [InlineData("closesAt")]
    public void Process_TimeOnlyProperty_IsTimeLocalWithPattern(string propertyName)
    {
        var property = Schema.Properties[propertyName];

        property.Format.ShouldBe("time-local");
        property.Pattern.ShouldBe(LocalTimePattern);
        property.Type.HasFlag(JsonObjectType.String).ShouldBeTrue();
    }

    [Fact]
    public void Process_NullableTimeOnlyProperty_StaysNullable()
    {
        Schema.Properties["closesAt"].IsNullable(SchemaType.OpenApi3).ShouldBeTrue();
        Schema.Properties["opensAt"].IsNullable(SchemaType.OpenApi3).ShouldBeFalse();
    }

    [Fact]
    public void TimeOnly_IsWrittenWithSecondsAndAFractionOnlyWhenThereIsOne()
    {
        JsonSerializer.Serialize(new TimeOnly(9, 0), BlueprintJsonOptions.Default).ShouldBe("\"09:00:00\"");
        JsonSerializer.Serialize(new TimeOnly(9, 0, 0, 500), BlueprintJsonOptions.Default).ShouldBe("\"09:00:00.5000000\"");
    }

    [Theory]
    [InlineData(9, 0, 0, 0)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(23, 59, 59, 999)]
    public void TimeOnlyPattern_MatchesWhatSystemTextJsonWrites(int hour, int minute, int second, int millisecond)
    {
        var written = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(new TimeOnly(hour, minute, second, millisecond), BlueprintJsonOptions.Default))!;

        IsMatch(written, LocalTimePattern).ShouldBeTrue(written);
    }

    [Theory]
    [InlineData("09:00:00", true)]
    [InlineData("09:00:00.5", true)]
    [InlineData("09:00:00.1234567", true)]
    [InlineData("09:00:00Z", false)]
    [InlineData("09:00:00+01:00", false)]
    [InlineData("09:00:00.12345678", false)]
    [InlineData("1.09:00:00", false)]
    public void TimeOnlyPattern_MatchesOnlyValuesSystemTextJsonReads(string value, bool expected)
    {
        IsMatch(value, LocalTimePattern).ShouldBe(expected);
        ReadsAsTimeOnly(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("09:00")]
    [InlineData("9:00:00")]
    public void TimeOnlyPattern_LeavesOutShortFormsSystemTextJsonAlsoReads(string value)
    {
        // The pattern describes the form the API writes, so a client that sends it is always understood.
        ReadsAsTimeOnly(value).ShouldBeTrue();
        IsMatch(value, LocalTimePattern).ShouldBeFalse();
    }

    [Theory]
    [InlineData(9, 30, 0)]
    [InlineData(9, 30, 500)]
    public void TimeOnlyPattern_MatchesTheExamplesNewtonsoftWrites(int hour, int minute, int millisecond)
    {
        // NSwag writes request and response examples into the document with Newtonsoft.Json.
        var example = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(Newtonsoft.Json.JsonConvert.SerializeObject(new TimeOnly(hour, minute, 0, millisecond)))!;

        IsMatch(example, LocalTimePattern).ShouldBeTrue(example);
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
        IsMatch(value, Schema.Properties["rentalLength"].Pattern!).ShouldBe(expected);
    }

    [Fact]
    public void Process_DateOnlyProperty_KeepsDateFormat()
    {
        Schema.Properties["purchaseDate"].Format.ShouldBe(JsonFormatStrings.Date);
    }

    private static bool IsMatch(string value, string pattern)
        => Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static bool ReadsAsTimeOnly(string value)
    {
        try
        {
            JsonSerializer.Deserialize<TimeOnly>(JsonSerializer.Serialize(value), BlueprintJsonOptions.Default);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonSchema CreateSchema()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings { SchemaType = SchemaType.OpenApi3 };
        settings.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        settings.SchemaProcessors.Add(new TemporalSchemaProcessor());
        return JsonSchema.FromType<Contract>(settings);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Only inspected by the schema generator.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S1144:Unused private types or members should be removed", Justification = "The properties are read by the schema generator through reflection.")]
    private sealed class Contract
    {
        public DateTime StartLocal { get; set; }

        public DateTime? EndLocal { get; set; }

        public TimeOnly OpensAt { get; set; }

        public TimeOnly? ClosesAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public TimeSpan RentalLength { get; set; }

        public TimeSpan? OptionalLength { get; set; }

        public DateOnly PurchaseDate { get; set; }
    }
}
