namespace Csag.Blueprint.Application.UnitTests.TableView;

using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Domain.Time;

/// <summary>
/// Tests <see cref="TableViewColumnDefinition{TEntity, TDto}.WithTimeZoneFrom"/>: a local reading column
/// whose zone comes from its row (a boat-show stand in Miami listed next to stands in Zurich) exposes the
/// zone column in its metadata, while its filters keep comparing readings as they read.
/// </summary>
public sealed class TableViewRowTimeZoneTests
{
    private static readonly StandTableViewDefinition Definition = new();

    [Fact]
    public void Metadata_ColumnWithRowZone_NamesTheZoneColumn()
    {
        var metadata = Definition.Metadata.ToDictionary(column => column.Name);

        metadata[nameof(StandDto.OpensLocal)].TimeZoneColumn.ShouldBe(nameof(StandDto.TimeZoneId));
        metadata[nameof(StandDto.OpensLocal)].DataType.ShouldBe("localDateTime");
    }

    [Fact]
    public void Metadata_ColumnsWithoutRowZone_HaveNoZoneColumn()
    {
        var metadata = Definition.Metadata.ToDictionary(column => column.Name);

        metadata[nameof(StandDto.ClosesLocal)].TimeZoneColumn.ShouldBeNull("a local reading defaults to the tenant zone");
        metadata[nameof(StandDto.BookedAt)].TimeZoneColumn.ShouldBeNull();
        metadata[nameof(StandDto.TimeZoneId)].TimeZoneColumn.ShouldBeNull();
    }

    [Fact]
    public void Metadata_SerializesZoneColumnForClients()
    {
        var opens = Definition.Metadata.Single(column => column.Name == nameof(StandDto.OpensLocal));

        var json = JsonSerializer.Serialize(opens, BlueprintJsonOptions.Default);

        json.ShouldContain("\"timeZoneColumn\":\"TimeZoneId\"");
    }

    [Fact]
    public void Metadata_ZoneFromPropertyThatIsNotAColumn_Throws()
    {
        var definition = new MissingZoneColumnDefinition();

        var exception = Should.Throw<InvalidOperationException>(() => definition.Metadata);

        exception.Message.ShouldContain("'TimeZoneId', which is not a column");
    }

    [Fact]
    public void WithTimeZoneFrom_OnInstantColumn_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new RowZoneOnDefinition(dto => dto.BookedAt))
            .Message.ShouldContain("'instant' column");
    }

    [Fact]
    public void WithTimeZoneFrom_OnDateColumn_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new RowZoneOnDefinition(dto => dto.ShowDate))
            .Message.ShouldContain("'date' column");
    }

    [Fact]
    public void WithTimeZoneFrom_SelectorThatIsNotAProperty_Throws()
    {
        Should.Throw<ArgumentException>(() => new NonPropertySelectorDefinition());
    }

    [Theory]
    [InlineData("2026-10-24T23:59:59", "Europe/Zurich", false)]
    [InlineData("2026-10-25T00:00:00", "America/New_York", true)]
    [InlineData("2026-10-25T23:30:00", "America/New_York", true)]
    [InlineData("2026-10-25T23:30:00", "Europe/Zurich", true)]
    [InlineData("2026-10-26T00:00:00", "Europe/Zurich", false)]
    public void GetFilterExpression_DateRangeOnColumnWithRowZone_ComparesReadingsAsTheyRead(string opensLocal, string rowZone, bool expected)
    {
        var stand = new Stand
        {
            OpensLocal = DateTime.Parse(opensLocal, System.Globalization.CultureInfo.InvariantCulture),
            TimeZoneId = rowZone,
        };

        // Neither the row's zone nor the request's zone shifts a local reading.
        foreach (var requestZone in new[] { TimeZoneInfo.Utc, FindZone("Pacific/Kiritimati") })
        {
            var expression = Definition.GetFilterExpression(nameof(StandDto.OpensLocal), "2026-10-25,2026-10-25", requestZone);

            expression.ShouldNotBeNull();
            expression.Compile()(stand).ShouldBe(expected);
        }
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S1144:Unused private types or members should be removed", Justification = "Columns without an entity selector read these properties through reflection.")]
    private sealed class Stand
    {
        public DateTime OpensLocal { get; set; }

        public DateTime ClosesLocal { get; set; }

        public DateTimeOffset BookedAt { get; set; }

        public DateOnly ShowDate { get; set; }

        public string TimeZoneId { get; set; } = string.Empty;
    }

    /// <summary>
    /// The DTO shape is only ever inspected through property-accessor expressions and never
    /// materialized, so every property carries an initializer to count as assigned.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Referenced via generic table view expressions.")]
    private sealed class StandDto
    {
        public DateTime OpensLocal { get; set; } = DateTime.MaxValue;

        public DateTime ClosesLocal { get; set; } = DateTime.MaxValue;

        public DateTimeOffset BookedAt { get; set; } = DateTimeOffset.MaxValue;

        public DateOnly ShowDate { get; set; } = DateOnly.MaxValue;

        public string TimeZoneId { get; set; } = string.Empty;

        public string Label() => this.TimeZoneId;
    }

    private sealed class StandTableViewDefinition : TableViewDefinition<Stand, StandDto>
    {
        public StandTableViewDefinition()
        {
            this.Column(dto => dto.OpensLocal)
                .Filterable(TableViewFilterOperator.DateRange)
                .WithTimeZoneFrom(dto => dto.TimeZoneId);
            this.Column(dto => dto.ClosesLocal);
            this.Column(dto => dto.BookedAt);
            this.Column(dto => dto.TimeZoneId);
        }
    }

    private sealed class MissingZoneColumnDefinition : TableViewDefinition<Stand, StandDto>
    {
        public MissingZoneColumnDefinition()
        {
            this.Column(dto => dto.OpensLocal).WithTimeZoneFrom(dto => dto.TimeZoneId);
        }
    }

    private sealed class RowZoneOnDefinition : TableViewDefinition<Stand, StandDto>
    {
        public RowZoneOnDefinition(System.Linq.Expressions.Expression<Func<StandDto, DateTimeOffset>> column)
        {
            this.Column(column).WithTimeZoneFrom(dto => dto.TimeZoneId);
        }

        public RowZoneOnDefinition(System.Linq.Expressions.Expression<Func<StandDto, DateOnly>> column)
        {
            this.Column(column).WithTimeZoneFrom(dto => dto.TimeZoneId);
        }
    }

    private sealed class NonPropertySelectorDefinition : TableViewDefinition<Stand, StandDto>
    {
        public NonPropertySelectorDefinition()
        {
            this.Column(dto => dto.OpensLocal).WithTimeZoneFrom(dto => dto.Label());
        }
    }
}
