namespace Csag.Blueprint.Infrastructure.Database.Conventions;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Applies the contract-driven blueprint conventions once the model is complete.
/// </summary>
/// <remarks>
/// <para>
/// Running at model finalization means every entity type is covered, however it entered the model:
/// a <c>DbSet</c>, a configuration applied after <c>base.OnModelCreating</c> (the usual shape of
/// <c>ApplyConfigurationsFromAssembly</c>), an owned type or a join entity. Every setting is applied at
/// convention level, so anything the application configured explicitly or through data annotations
/// still wins even though this runs last.
/// </para>
/// <para>
/// It is a model convention rather than a replaced <see cref="IModelCustomizer"/> because registering
/// that service requires modifying <c>DbContextOptions</c> from <c>OnConfiguring</c>, which EF Core
/// forbids once <c>DbContext</c> pooling is enabled — the shape <c>AddPooledDbContextFactory</c>
/// produces: <c>'OnConfiguring' cannot be used to modify DbContextOptions when DbContext pooling is enabled.</c>
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

        MultiTenancyModelBuilderExtensions.ConfigureBlueprintMultiTenancy<TAppTenant, BlueprintDbContext<TAppTenant, TAppUser, TAppRole>>(
            modelBuilder, this.context, nameof(this.context.CurrentTenantId), addTenantForeignKey: true);
        ContractModelBuilderExtensions.ConfigureContractConstraints(modelBuilder);
        LocalizationModelBuilderExtensions.ConfigureLocalizedTextConventions(modelBuilder);
        EntityFilteringModelBuilderExtensions.ConfigureEntityFiltering(modelBuilder);

        // NEWSEQUENTIALID() is a SQL Server function, so this convention only applies there: other
        // providers accept the DDL and then fail every insert. Runs last, so relationships the
        // conventions above add are in place before key properties are told apart from foreign keys.
        if (this.context.Database.IsSqlServer())
        {
            KeyConventionModelBuilderExtensions.ConfigureGuidPrimaryKeyDefaults(modelBuilder);
        }
    }
}
