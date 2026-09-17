namespace Csag.Blueprint.Application.UnitTests.TableView;

using System.Globalization;
using Csag.Blueprint.Application.TableView;

/// <summary>
/// Pins the edge matrix of <see cref="TableViewDefinition{TEntity, TDto}.GetFilterExpression"/>.
/// Filter values arrive as untrusted request input, so every malformed value must yield a null
/// expression (i.e. no filter applied) instead of throwing, and range/date bounds must stay
/// inclusive on both ends.
/// </summary>
public sealed class TableViewFilterExpressionTests
{
    private static readonly VehicleTableViewDefinition Definition = new();

    /// <summary>
    /// Enum column type for the test entity; the member order fixes the numeric values the
    /// numeric-string filter tests rely on.
    /// </summary>
    private enum VehicleKind
    {
        /// <summary>A car (numeric value 0).</summary>
        Car,

        /// <summary>A truck (numeric value 1).</summary>
        Truck,

        /// <summary>A bus (numeric value 2).</summary>
        Bus,
    }

    [Theory]
    [InlineData("Missing", "x")] // unknown column
    [InlineData("Notes", "x")] // column never marked filterable
    [InlineData("Synthetic", "x")] // filterable, but no matching entity property to filter on
    [InlineData("Seats", "1")] // Contains is only supported for string columns
    [InlineData("Doors", "abc")] // unparsable int
    [InlineData("Doors", "10.5")] // int columns reject decimal input
    [InlineData("Doors", "2147483648")] // int overflow
    [InlineData("PricePerHour", "ten")] // unparsable decimal
    [InlineData("IsElectric", "maybe")] // invalid bool literal for Equals
    [InlineData("IsActive", "maybe")] // invalid bool literal
    [InlineData("Capacity", "abc-def")] // unparsable range bounds
    [InlineData("Capacity", "10")] // range without separator
    [InlineData("Capacity", "")] // empty range
    [InlineData("RetiredOn", "1-10")] // Range only supports number columns, not dates
    [InlineData("Category", ",,")] // In-list with only empty entries
    [InlineData("Mileage", "x,y")] // In-list where no entry parses
    [InlineData("Kind", "hovercraft")] // bogus enum name
    [InlineData("AcquiredOn", "2024-01-10")] // date range without separator
    [InlineData("AcquiredOn", "foo,bar")] // unparsable date bounds
    [InlineData("Id", "not-a-guid")]
    [InlineData("CreatedAt", "not-a-date")]
    public void GetFilterExpression_ForInvalidColumnOrValue_ReturnsNull(string columnName, string filterValue)
    {
        Definition.GetFilterExpression(columnName, filterValue).ShouldBeNull();
    }

    [Theory]
    [InlineData("Cargo", true)]
    [InlineData("go Ma", true)]
    [InlineData("Steam", false)]
    public void GetFilterExpression_ContainsOnStringColumn_MatchesSubstrings(string filterValue, bool expected)
    {
        Matches("Name", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("4", true)]
    [InlineData("5", false)]
    public void GetFilterExpression_EqualsOnIntColumn_ComparesParsedValue(string filterValue, bool expected)
    {
        Matches("Doors", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("10.5", true)]
    [InlineData("10.50", true)] // decimal equality is numeric, trailing zeros do not matter
    [InlineData("10.6", false)]
    public void GetFilterExpression_EqualsOnDecimalColumn_ComparesParsedValue(string filterValue, bool expected)
    {
        Matches("PricePerHour", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_EqualsOnGuidColumn_IsCaseInsensitive()
    {
        Matches("Id", "7C9E6679-7425-40DE-944B-E07FC1F90AE7", CreateVehicle()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("2024-05-01T10:00:00+02:00", true)]
    [InlineData("2024-05-01T08:00:00+00:00", true)] // DateTimeOffset equality compares instants, not offsets
    [InlineData("2024-05-01T09:00:00+02:00", false)]
    public void GetFilterExpression_EqualsOnDateTimeOffsetColumn_ComparesInstants(string filterValue, bool expected)
    {
        Matches("CreatedAt", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    public void GetFilterExpression_BooleanFilter_ComparesParsedValue(string filterValue, bool expected)
    {
        Matches("IsActive", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("false", true)]
    [InlineData("False", true)]
    [InlineData("true", false)]
    public void GetFilterExpression_EqualsOnBoolColumn_ComparesParsedValue(string filterValue, bool expected)
    {
        Matches("IsElectric", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(15, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void GetFilterExpression_NumericRange_BoundsAreInclusive(int capacity, bool expected)
    {
        var vehicle = CreateVehicle();
        vehicle.Capacity = capacity;

        Matches("Capacity", "10-20", vehicle).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_NumericRangeWithOpenMax_FiltersOnMinimumOnly()
    {
        var vehicle = CreateVehicle();

        vehicle.Capacity = 10;
        Matches("Capacity", "10-", vehicle).ShouldBeTrue();

        vehicle.Capacity = 9;
        Matches("Capacity", "10-", vehicle).ShouldBeFalse();
    }

    [Fact]
    public void GetFilterExpression_NumericRangeWithOpenMin_FiltersOnMaximumOnly()
    {
        var vehicle = CreateVehicle();

        vehicle.Capacity = 20;
        Matches("Capacity", "-20", vehicle).ShouldBeTrue();

        vehicle.Capacity = 21;
        Matches("Capacity", "-20", vehicle).ShouldBeFalse();
    }

    [Theory]
    [InlineData(-6, false)]
    [InlineData(-5, true)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void GetFilterExpression_NumericRangeWithNegativeMinimum_BoundsAreInclusive(int capacity, bool expected)
    {
        var vehicle = CreateVehicle();
        vehicle.Capacity = capacity;

        // The leading '-' belongs to the minimum bound; the second '-' separates the bounds.
        Matches("Capacity", "-5-10", vehicle).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_NumericRangeWithNegativeBounds_ComparesParsedBounds()
    {
        var vehicle = CreateVehicle();

        vehicle.Capacity = -3;
        Matches("Capacity", "-5--1", vehicle).ShouldBeTrue();

        vehicle.Capacity = 0;
        Matches("Capacity", "-5--1", vehicle).ShouldBeFalse();
    }

    [Fact]
    public void GetFilterExpression_NumericRangeOnDecimalColumn_ComparesParsedBounds()
    {
        var vehicle = CreateVehicle();

        vehicle.Weight = 12.5m;
        Matches("Weight", "9.5-20.25", vehicle).ShouldBeTrue();

        vehicle.Weight = 9.4m;
        Matches("Weight", "9.5-20.25", vehicle).ShouldBeFalse();
    }

    [Theory]
    [InlineData("van", true)]
    [InlineData(" van , bus ", true)] // entries are trimmed
    [InlineData("van,,", true)] // empty entries are dropped
    [InlineData("car,bus", false)]
    public void GetFilterExpression_InOnStringColumn_SplitsTrimsAndDropsEmptyEntries(string filterValue, bool expected)
    {
        Matches("Category", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_InOnNumberColumn_SkipsUnparsableEntries()
    {
        var vehicle = CreateVehicle();

        vehicle.Mileage = 3;
        Matches("Mileage", "1,x,3", vehicle).ShouldBeTrue();

        vehicle.Mileage = 2;
        Matches("Mileage", "1,x,3", vehicle).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Truck", true)]
    [InlineData("truck", true)] // enum name matching is case-insensitive
    [InlineData("1", true)] // Enum.TryParse also accepts the numeric value of a member
    [InlineData("Car", false)]
    public void GetFilterExpression_EnumFilter_MatchesByNameOrNumericValue(string filterValue, bool expected)
    {
        Matches("Kind", filterValue, CreateVehicle()).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_EnumFilterWithUndefinedNumericValue_ReturnsNull()
    {
        // Numeric strings only pass when they name a defined member, so an undefined value is
        // rejected like any other malformed filter input.
        Definition.GetFilterExpression("Kind", "999").ShouldBeNull();
    }

    [Theory]
    [InlineData("2024-01-09", false)]
    [InlineData("2024-01-10", true)]
    [InlineData("2024-01-15", true)]
    [InlineData("2024-01-20", true)]
    [InlineData("2024-01-21", false)]
    public void GetFilterExpression_DateRangeOnDateOnlyColumn_BoundsAreInclusive(string acquiredOn, bool expected)
    {
        var vehicle = CreateVehicle();
        vehicle.AcquiredOn = DateOnly.Parse(acquiredOn, CultureInfo.InvariantCulture);

        Matches("AcquiredOn", "2024-01-10,2024-01-20", vehicle).ShouldBe(expected);
    }

    [Fact]
    public void GetFilterExpression_DateRangeWithOpenEnd_FiltersOnStartOnly()
    {
        var vehicle = CreateVehicle();

        vehicle.AcquiredOn = new DateOnly(2024, 1, 10);
        Matches("AcquiredOn", "2024-01-10,", vehicle).ShouldBeTrue();

        vehicle.AcquiredOn = new DateOnly(2024, 1, 9);
        Matches("AcquiredOn", "2024-01-10,", vehicle).ShouldBeFalse();
    }

    [Fact]
    public void GetFilterExpression_DateRangeWithOpenStart_FiltersOnEndOnly()
    {
        var vehicle = CreateVehicle();

        vehicle.AcquiredOn = new DateOnly(2024, 1, 20);
        Matches("AcquiredOn", ",2024-01-20", vehicle).ShouldBeTrue();

        vehicle.AcquiredOn = new DateOnly(2024, 1, 21);
        Matches("AcquiredOn", ",2024-01-20", vehicle).ShouldBeFalse();
    }

    [Fact]
    public void GetFilterExpression_DateRangeOnDateTimeOffsetColumn_ComparesParsedBounds()
    {
        var vehicle = CreateVehicle();

        vehicle.LastServiceAt = new DateTimeOffset(2024, 6, 15, 8, 30, 0, TimeSpan.Zero);
        Matches("LastServiceAt", "2024-06-01T00:00:00Z,2024-07-01T00:00:00Z", vehicle).ShouldBeTrue();

        vehicle.LastServiceAt = new DateTimeOffset(2024, 7, 2, 0, 0, 0, TimeSpan.Zero);
        Matches("LastServiceAt", "2024-06-01T00:00:00Z,2024-07-01T00:00:00Z", vehicle).ShouldBeFalse();
    }

    private static bool Matches(string columnName, string filterValue, Vehicle vehicle)
    {
        var expression = Definition.GetFilterExpression(columnName, filterValue);
        expression.ShouldNotBeNull();
        return expression.Compile()(vehicle);
    }

    private static Vehicle CreateVehicle()
    {
        return new Vehicle
        {
            Id = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7", CultureInfo.InvariantCulture),
            Name = "Cargo Master",
            Category = "van",
            Doors = 4,
            Capacity = 15,
            Seats = 5,
            Weight = 12.5m,
            PricePerHour = 10.5m,
            Mileage = 3,
            IsActive = true,
            IsElectric = false,
            Kind = VehicleKind.Truck,
            AcquiredOn = new DateOnly(2024, 1, 15),
            RetiredOn = new DateOnly(2030, 1, 1),
            CreatedAt = new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.FromHours(2)),
            LastServiceAt = new DateTimeOffset(2024, 6, 15, 8, 30, 0, TimeSpan.Zero),
            Notes = "well maintained",
        };
    }

    private sealed class Vehicle
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int Doors { get; set; }

        public int Capacity { get; set; }

        public int Seats { get; set; }

        public decimal Weight { get; set; }

        public decimal PricePerHour { get; set; }

        public int Mileage { get; set; }

        public bool IsActive { get; set; }

        public bool IsElectric { get; set; }

        public VehicleKind Kind { get; set; }

        public DateOnly AcquiredOn { get; set; }

        public DateOnly RetiredOn { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset LastServiceAt { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// The DTO shape is only ever inspected through property-accessor expressions and never
    /// materialized, so every property carries an initializer to count as assigned.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Referenced via generic table view expressions.")]
    private sealed class VehicleDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int Doors { get; set; } = 1;

        public int Capacity { get; set; } = 1;

        public int Seats { get; set; } = 1;

        public decimal Weight { get; set; } = 1m;

        public decimal PricePerHour { get; set; } = 1m;

        public int Mileage { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public bool IsElectric { get; set; } = true;

        public VehicleKind Kind { get; set; } = VehicleKind.Truck;

        public DateOnly AcquiredOn { get; set; } = DateOnly.MaxValue;

        public DateOnly RetiredOn { get; set; } = DateOnly.MaxValue;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MaxValue;

        public DateTimeOffset LastServiceAt { get; set; } = DateTimeOffset.MaxValue;

        public string Notes { get; set; } = string.Empty;

        public string Synthetic { get; set; } = string.Empty;
    }

    private sealed class VehicleTableViewDefinition : TableViewDefinition<Vehicle, VehicleDto>
    {
        public VehicleTableViewDefinition()
        {
            this.Column(d => d.Id).Filterable(TableViewFilterOperator.Equals);
            this.Column(d => d.Name).Filterable(TableViewFilterOperator.Contains);
            this.Column(d => d.Category).Filterable(TableViewFilterOperator.In);
            this.Column(d => d.Doors).Filterable(TableViewFilterOperator.Equals);
            this.Column(d => d.Capacity).Filterable(TableViewFilterOperator.Range);
            this.Column(d => d.Seats).Filterable(TableViewFilterOperator.Contains);
            this.Column(d => d.Weight).Filterable(TableViewFilterOperator.Range);
            this.Column(d => d.PricePerHour).Filterable(TableViewFilterOperator.Equals);
            this.Column(d => d.Mileage).Filterable(TableViewFilterOperator.In);
            this.Column(d => d.IsActive).Filterable(TableViewFilterOperator.Boolean);
            this.Column(d => d.IsElectric).Filterable(TableViewFilterOperator.Equals);
            this.Column(d => d.Kind).Filterable(TableViewFilterOperator.Enum);
            this.Column(d => d.AcquiredOn).Filterable(TableViewFilterOperator.DateRange);
            this.Column(d => d.RetiredOn).Filterable(TableViewFilterOperator.Range);
            this.Column(d => d.CreatedAt).Filterable(TableViewFilterOperator.Equals);
            this.Column(d => d.LastServiceAt).Filterable(TableViewFilterOperator.DateRange);
            this.Column(d => d.Notes);

            // The DTO exposes Synthetic but the entity has no such property, so the column ends up
            // without an entity expression and can never produce a filter.
            this.Column(d => d.Synthetic).Filterable(TableViewFilterOperator.Contains);
        }
    }
}
