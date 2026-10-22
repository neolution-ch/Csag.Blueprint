namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Web.Validation;
using FastEndpoints;
using FluentValidation;

/// <summary>
/// Shared validator for table view export requests.
/// </summary>
public sealed class TableViewExportRequestValidator : Validator<TableViewExportRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewExportRequestValidator"/> class.
    /// </summary>
    public TableViewExportRequestValidator()
    {
        this.RuleFor(x => x.TimeZone)
            .MustBeIanaTimeZone()
            .WithMessage(TableViewRequestTimeZone.InvalidMessage);
    }
}
