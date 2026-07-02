namespace Csag.Blueprint.Web.TableView;

using Csag.Blueprint.Application.TableView;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Compatibility wrapper for EF-backed table views that still build queries from a DbContext.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDto">The DTO type for projection.</typeparam>
/// <typeparam name="TDefinition">The concrete table view definition type.</typeparam>
#pragma warning disable S2436 // Type parameters are intrinsic to this generic base pattern
public abstract class TableViewDataEndpointBase<TContext, TEntity, TDto, TDefinition>
#pragma warning restore S2436
    : TableViewDataEndpointBase<TEntity, TDto, TDefinition>
    where TContext : DbContext
    where TEntity : class
    where TDto : class, new()
    where TDefinition : class, ITableViewDefinition<TEntity, TDto>
{
    private readonly TContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewDataEndpointBase{TContext, TEntity, TDto, TDefinition}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="tableViewExecutor">The table view query executor.</param>
    /// <param name="definition">The table view definition.</param>
    protected TableViewDataEndpointBase(
        TContext context,
        ITableViewExecutor tableViewExecutor,
        TDefinition definition)
        : base(tableViewExecutor, definition)
    {
        this.context = context;
    }

    /// <summary>
    /// Gets the database context for building queries.
    /// </summary>
    protected TContext Context => this.context;

    /// <inheritdoc/>
    protected sealed override IQueryable<TEntity> BuildQuery(TableViewDataRequest request)
    {
        return this.BuildQuery();
    }

    /// <summary>
    /// Builds the base <see cref="IQueryable{TEntity}"/> for the table view, including any necessary
    /// navigation property includes. The query should use <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>.
    /// </summary>
    /// <returns>The base queryable for the entity.</returns>
    protected abstract IQueryable<TEntity> BuildQuery();
}
