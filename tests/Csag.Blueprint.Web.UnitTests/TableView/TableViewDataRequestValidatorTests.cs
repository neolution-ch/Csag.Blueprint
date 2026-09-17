namespace Csag.Blueprint.Web.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Web.TableView;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="TableViewDataRequestValidator"/>.
/// </summary>
public sealed class TableViewDataRequestValidatorTests
{
    private readonly TableViewDataRequestValidator validator;

    public TableViewDataRequestValidatorTests()
    {
        this.validator = new TableViewDataRequestValidator();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Validate_PageWithValidValue_Passes(int page)
    {
        // Arrange
        var request = new TableViewDataRequest { Page = page };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageWithInvalidValue_Fails(int page)
    {
        // Arrange
        var request = new TableViewDataRequest { Page = page };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Validate_PageSizeWithValidValue_Passes(int pageSize)
    {
        // Arrange
        var request = new TableViewDataRequest { PageSize = pageSize };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_PageSizeWithInvalidValue_Fails(int pageSize)
    {
        // Arrange
        var request = new TableViewDataRequest { PageSize = pageSize };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_DefaultRequest_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest();

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NullSortColumns_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest { SortColumns = null };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SortColumns);
    }

    [Fact]
    public void Validate_EmptySortColumns_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest { SortColumns = [] };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SortColumns);
    }

    [Fact]
    public void Validate_SingleSortColumn_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest
        {
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MultipleSortColumns_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest
        {
            SortColumns =
            [
                new SortColumn { ColumnName = "Type", Direction = SortDirection.Asc },
                new SortColumn { ColumnName = "Name", Direction = SortDirection.Desc },
            ],
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_SortColumnWithEmptyName_Fails()
    {
        // Arrange
        var request = new TableViewDataRequest
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
        var request = new TableViewDataRequest
        {
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = (SortDirection)99 }],
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Direction", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequestWithValidFilters_Passes()
    {
        // Arrange
        var request = new TableViewDataRequest
        {
            Page = 1,
            PageSize = 25,
            SortColumns = [new SortColumn { ColumnName = "Name", Direction = SortDirection.Asc }],
            Filters = new Dictionary<string, string>
            {
                { "Name", "Test" },
                { "IsAvailable", "true" },
            },
        };

        // Act
        var result = this.validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
