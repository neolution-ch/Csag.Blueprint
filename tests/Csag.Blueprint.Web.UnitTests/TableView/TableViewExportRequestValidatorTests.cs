namespace Csag.Blueprint.Web.UnitTests.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Web.TableView;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="TableViewExportRequestValidator"/>.
/// </summary>
public sealed class TableViewExportRequestValidatorTests
{
    private readonly TableViewExportRequestValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("America/New_York")]
    public void Validate_TimeZoneOmittedOrIana_Passes(string? timeZone)
    {
        var result = this.validator.TestValidate(new TableViewExportRequest { TimeZone = timeZone });

        result.ShouldNotHaveValidationErrorFor(x => x.TimeZone);
    }

    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("Europe/Zürich")]
    public void Validate_TimeZoneNotIana_Fails(string timeZone)
    {
        var result = this.validator.TestValidate(new TableViewExportRequest { TimeZone = timeZone });

        result.ShouldHaveValidationErrorFor(x => x.TimeZone)
            .WithErrorMessage("TimeZone must be an IANA time zone id, such as Europe/Zurich.");
    }
}
