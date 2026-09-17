namespace Csag.Blueprint.Infrastructure.Database.Conventions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Applies the contract-driven blueprint conventions after <c>OnModelCreating</c> has run in full.
/// </summary>
/// <remarks>
/// <para>
/// The conventions used to be applied inline in <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}"/>'s
/// <c>OnModelCreating</c>, which meant they only saw the entity types discovered up to that point. An
/// application registering a type <b>after</b> its <c>base.OnModelCreating</c> call — the usual shape of
/// <c>ApplyConfigurationsFromAssembly</c>, owned types and join entities, none of which need a
/// <c>DbSet</c> — silently received none of them. For <see cref="Domain.Contracts.ISoftDeletable"/> that
/// meant no global query filter at all, so soft-deleted rows came back in every query.
/// </para>
/// <para>
/// <see cref="IModelCustomizer"/> is the documented extension point that runs <c>OnModelCreating</c>,
/// so customizing after <see cref="ModelCustomizer.Customize"/> sees the finished model no matter how or
/// when a type was registered. It is used in preference to an <c>IModelFinalizingConvention</c> because a
/// convention only receives EF Core's internal model builder, and wrapping that in a public
/// <see cref="ModelBuilder"/> requires an API marked <c>EF1001</c> — internal, and removable without
/// notice in any release.
/// </para>
/// </remarks>
public class BlueprintModelCustomizer : ModelCustomizer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintModelCustomizer"/> class.
    /// </summary>
    /// <param name="dependencies">The service dependencies.</param>
    public BlueprintModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    /// Builds the model, then applies the blueprint conventions to the completed result.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="context">The context the model is being built for.</param>
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        // Runs OnModelCreating, the application's override included.
        base.Customize(modelBuilder, context);

        if (context is IBlueprintModelConventions conventions)
        {
            conventions.ApplyBlueprintConventions(modelBuilder);
        }
    }
}
