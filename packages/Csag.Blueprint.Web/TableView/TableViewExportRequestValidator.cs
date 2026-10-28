namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using Csag.Blueprint.Web.Validation;
using FastEndpoints;
using FluentValidation;

/// <summary>
/// Shared validator for table view export requests. A page, when requested, is bounded like a page of
/// <see cref="TableViewDataRequest"/>; an export without one is bounded by
/// <see cref="TableViewExportEndpointBase{TContext, TEntity, TDto, TDefinition}.MaxExportRows"/>.
/// </summary>
public sealed class TableViewExportRequestValidator : Validator<TableViewExportRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewExportRequestValidator"/> class.
    /// </summary>
    public TableViewExportRequestValidator()
    {
        this.RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1");

        this.RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        this.RuleFor(x => x.PageSize)
            .NotNull()
            .When(x => x.Page.HasValue)
            .WithMessage("PageSize is required when Page is set");

        this.RuleFor(x => x.Page)
            .NotNull()
            .When(x => x.PageSize.HasValue)
            .WithMessage("Page is required when PageSize is set");

        this.RuleFor(x => x.TimeZone)
            .MustBeIanaTimeZone()
            .WithMessage(TableViewRequestTimeZone.InvalidMessage);

        this.RuleForEach(x => x.SortColumns)
            .ChildRules(TableViewDataRequestValidator.AddSortColumnRules);
    }
}
