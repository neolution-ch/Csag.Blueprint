namespace Csag.Blueprint.Web.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Web.TableView;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="TableViewExportRequestValidator"/>.
/// </summary>
public sealed class TableViewExportRequestValidatorTests
{
    private readonly TableViewExportRequestValidator validator;

    public TableViewExportRequestValidatorTests()
    {
        this.validator = new TableViewExportRequestValidator();
    }

    [Fact]
    public void Validate_RequestWithoutPage_Passes()
    {
        // Arrange — an export of every matching row, bounded by the endpoint rather than the validator.
        var request = new TableViewExportRequest();

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 25)]
    [InlineData(1, 100)]
    public void Validate_PageWithinBounds_Passes(int page, int pageSize)
    {
        // Arrange
        var request = new TableViewExportRequest { Page = page, PageSize = pageSize };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_Fails(int page)
    {
        // Arrange
        var request = new TableViewExportRequest { Page = page, PageSize = 10 };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Validate_PageSizeOutOfBounds_Fails(int pageSize)
    {
        // Arrange
        var request = new TableViewExportRequest { Page = 1, PageSize = pageSize };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_PageWithoutPageSize_Fails()
    {
        // Arrange
        var request = new TableViewExportRequest { Page = 1 };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_PageSizeWithoutPage_Fails()
    {
        // Arrange
        var request = new TableViewExportRequest { PageSize = 10 };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Validate_SortColumnWithEmptyName_Fails()
    {
        // Arrange — the sort column rules are shared with TableViewDataRequestValidator.
        var request = new TableViewExportRequest
        {
            SortColumns = [new SortColumn { ColumnName = string.Empty, Direction = SortDirection.Asc }],
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("ColumnName", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SortColumnWithInvalidDirection_Fails()
    {
        // Arrange — undefined enum value.
        var request = new TableViewExportRequest
        {
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = (SortDirection)99 }],
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Direction", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("America/New_York")]
    [InlineData("America/Coyhaique")] // newer than some hosts' time zone data; well-formed, so it passes
    public void Validate_TimeZoneOmittedOrIana_Passes(string? timeZone)
    {
        // Arrange
        var request = new TableViewExportRequest { TimeZone = timeZone };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TimeZone);
    }

    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("Europe/Zürich")]
    public void Validate_TimeZoneNotIana_Fails(string timeZone)
    {
        // Arrange
        var request = new TableViewExportRequest { TimeZone = timeZone };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeZone)
            .WithErrorMessage("TimeZone must be an IANA time zone id, such as Europe/Zurich.");
    }

    [Fact]
    public void Validate_TimeZoneLongerThan64Characters_Fails()
    {
        // Arrange — well-formed in every other respect, but no real id comes close.
        var request = new TableViewExportRequest { TimeZone = "Europe/" + new string('a', 58) };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeZone);
    }
}
