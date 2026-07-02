namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using FastEndpoints;
using FluentValidation;

/// <summary>
/// Shared validator for table view data requests.
/// </summary>
public sealed class TableViewDataRequestValidator : Validator<TableViewDataRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewDataRequestValidator"/> class.
    /// </summary>
    public TableViewDataRequestValidator()
    {
        this.RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1");

        this.RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        this.RuleForEach(x => x.SortColumns)
            .ChildRules(s =>
            {
                s.RuleFor(c => c.ColumnName)
                    .NotEmpty().WithMessage("Sort column name is required")
                    .MaximumLength(100).WithMessage("Sort column name cannot exceed 100 characters");

                s.RuleFor(c => c.Direction)
                    .IsInEnum().WithMessage("Sort direction must be a valid value");
            });
    }
}
