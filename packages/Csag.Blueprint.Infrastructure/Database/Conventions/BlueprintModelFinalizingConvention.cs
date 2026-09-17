namespace Csag.Blueprint.Infrastructure.Database.Conventions;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Applies the contract-driven blueprint conventions once the model is complete.
/// </summary>
/// <remarks>
/// <para>
/// These conventions used to run inline in <c>OnModelCreating</c>, so they only saw the entity types
/// discovered up to that point. An application registering a type <b>after</b> its
/// <c>base.OnModelCreating</c> call — the usual shape of <c>ApplyConfigurationsFromAssembly</c>, owned
/// types and join entities, none of which need a <c>DbSet</c> — silently received none of them. For
/// <see cref="Domain.Contracts.ISoftDeletable"/> that meant no global query filter at all, so
/// soft-deleted rows came back in every query.
/// </para>
/// <para>
/// A model convention is used rather than a replaced <see cref="IModelCustomizer"/> because
/// registering that service requires modifying <c>DbContextOptions</c> from <c>OnConfiguring</c>,
/// which EF Core forbids outright once <c>DbContext</c> pooling is enabled — the shape
/// <c>AddPooledDbContextFactory</c> produces:
/// <c>'OnConfiguring' cannot be used to modify DbContextOptions when DbContext pooling is enabled.</c>
/// A convention touches no options and no service provider, so it works under pooling.
/// </para>
/// </remarks>
/// <typeparam name="TAppTenant">The concrete tenant entity type.</typeparam>
/// <typeparam name="TAppUser">The concrete user entity type.</typeparam>
/// <typeparam name="TAppRole">The concrete role entity type.</typeparam>
[SuppressMessage("SonarQube", "S2436", Justification = "Mirrors the three generic parameters of the BlueprintDbContext this convention configures.")]
public sealed class BlueprintModelFinalizingConvention<TAppTenant, TAppUser, TAppRole> : IModelFinalizingConvention
    where TAppTenant : BlueprintTenant
    where TAppUser : BlueprintUser
    where TAppRole : BlueprintRole
{
    private readonly BlueprintDbContext<TAppTenant, TAppUser, TAppRole> context;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BlueprintModelFinalizingConvention{TAppTenant, TAppUser, TAppRole}"/> class.
    /// </summary>
    /// <param name="context">
    /// The context being configured. It is handed to the multi-tenancy convention so the tenant filter
    /// closes over a <see cref="DbContext"/>-typed constant, which EF Core re-evaluates against the
    /// executing context per query rather than capturing the model-building instance.
    /// </param>
    public BlueprintModelFinalizingConvention(BlueprintDbContext<TAppTenant, TAppUser, TAppRole> context)
    {
        this.context = context;
    }

    /// <summary>
    /// Applies the blueprint conventions to the finalized model.
    /// </summary>
    /// <param name="modelBuilder">The convention-level model builder.</param>
    /// <param name="context">The convention context.</param>
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The conventions are written against the public ModelBuilder surface so applications that do
        // not derive from BlueprintDbContext can keep calling them directly. Wrapping the model being
        // finalized is the only way to reach them from here, and that constructor is EF-internal.
        // The suppression is deliberately this narrow: if EF Core ever removes it the build breaks
        // loudly rather than the conventions quietly not being applied.
#pragma warning disable EF1001 // Internal EF Core API usage.
        var builder = new ModelBuilder((IMutableModel)modelBuilder.Metadata);
#pragma warning restore EF1001

        builder.ConfigureBlueprintMultiTenancy<TAppTenant, BlueprintDbContext<TAppTenant, TAppUser, TAppRole>>(this.context);
        builder.ConfigureContractConstraints();
        builder.ConfigureLocalizedTextConventions();
        builder.ConfigureEntityFiltering();

        // Last, so entity types introduced by the conventions above are covered as well.
        builder.ConfigureGuidPrimaryKeyDefaults();
    }
}
