namespace Csag.Blueprint.Infrastructure.Database;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Database.Configurations;
using Csag.Blueprint.Infrastructure.Database.Conventions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// Blueprint base database context for Identity, multi-tenancy, and shared business entities.
/// Multi-tenancy is handled through TenantContext (ambient AsyncLocal) which is set by middleware.
/// </summary>
/// <typeparam name="TAppTenant">The concrete tenant entity type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
/// <typeparam name="TAppUser">The concrete user entity type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TAppRole">The concrete role entity type, must derive from <see cref="BlueprintRole"/>.</typeparam>
[SuppressMessage("SonarQube", "S1200", Justification = "A DbContext necessarily aggregates all entity type dependencies.")]
[SuppressMessage("SonarQube", "S2436", Justification = "Three generic parameters are required to support tenant, user, and role entity customization.")]
public class BlueprintDbContext<TAppTenant, TAppUser, TAppRole> : IdentityDbContext<TAppUser, TAppRole, Guid>, IDataProtectionKeyContext, IBlueprintModelConventions
    where TAppTenant : BlueprintTenant
    where TAppUser : BlueprintUser
    where TAppRole : BlueprintRole
{
    /// <summary>
    /// Stores the ambient tenant ID accessor as an instance field so that <see cref="CurrentTenantId"/> is a
    /// genuine instance property and satisfies static-member analyzers (CA1822). The delegate reads from
    /// <see cref="TenantContext"/> (an <see cref="System.Threading.AsyncLocal{T}"/>) on each invocation,
    /// returning the tenant for the current async execution context (i.e. the current request).
    /// </summary>
    private readonly Func<Guid?> tenantIdAccessor = () => TenantContext.Current;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public BlueprintDbContext(DbContextOptions<BlueprintDbContext<TAppTenant, TAppUser, TAppRole>> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintDbContext{TAppTenant, TAppUser, TAppRole}"/> class.
    /// Used by derived contexts that pass their own typed <see cref="DbContextOptions{T}"/>.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    protected BlueprintDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the active authentication sessions.
    /// </summary>
    public DbSet<BlueprintActiveSession> ActiveSessions => this.Set<BlueprintActiveSession>();

    /// <summary>
    /// Gets the audit log entries.
    /// </summary>
    public DbSet<BlueprintAuditLog> AuditLogs => this.Set<BlueprintAuditLog>();

    /// <summary>
    /// Gets the resource access entries.
    /// </summary>
    public DbSet<BlueprintResourceAccess> ResourceAccess => this.Set<BlueprintResourceAccess>();

    /// <summary>
    /// Gets the service accounts.
    /// </summary>
    public DbSet<BlueprintServiceAccount> ServiceAccounts => this.Set<BlueprintServiceAccount>();

    /// <summary>
    /// Gets the active service-account sessions (server-side backing records for reference-style JWTs).
    /// </summary>
    public DbSet<BlueprintServiceAccountSession> ServiceAccountSessions => this.Set<BlueprintServiceAccountSession>();

    /// <summary>
    /// Gets the table view preferences.
    /// </summary>
    public DbSet<BlueprintTableViewPreference<TAppUser>> TableViewPreferences => this.Set<BlueprintTableViewPreference<TAppUser>>();

    /// <summary>
    /// Gets the application's concrete tenant entities.
    /// </summary>
    public DbSet<TAppTenant> Tenants => this.Set<TAppTenant>();

    /// <summary>
    /// Gets the translations.
    /// </summary>
    public DbSet<BlueprintTranslation> Translations => this.Set<BlueprintTranslation>();

    /// <summary>
    /// Gets the data protection keys.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => this.Set<DataProtectionKey>();

    /// <summary>
    /// Gets memberships between <typeparamref name="TAppUser"/> and <typeparamref name="TAppTenant"/>.
    /// </summary>
    public DbSet<BlueprintTenantMembership<TAppUser, TAppTenant>> TenantMemberships => this.Set<BlueprintTenantMembership<TAppUser, TAppTenant>>();

    /// <summary>
    /// Gets the tenant-scoped role assignments linking memberships to roles in the shared catalog.
    /// </summary>
    public DbSet<BlueprintTenantMembershipRole<TAppUser, TAppTenant>> TenantMembershipRoles => this.Set<BlueprintTenantMembershipRole<TAppUser, TAppTenant>>();

    /// <summary>
    /// Gets the tenant-scoped direct permission grants linking memberships to individual permissions.
    /// </summary>
    public DbSet<BlueprintTenantMembershipPermission<TAppUser, TAppTenant>> TenantMembershipPermissions => this.Set<BlueprintTenantMembershipPermission<TAppUser, TAppTenant>>();

    /// <summary>
    /// Gets the current tenant ID for the executing request from the ambient <see cref="TenantContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be <c>public</c> so EF Core can compile the property-accessor delegate from outside this assembly.
    /// </para>
    /// <para>
    /// Must be an <b>instance</b> property. EF Core's <c>ParameterExtractingExpressionVisitor</c> detects a
    /// <c>MemberExpression</c> on a <see cref="DbContext"/>-typed constant and re-evaluates it against the
    /// <b>current</b> executing context instance per query. Static member access does not receive this treatment
    /// and would be evaluated once and cached as a SQL parameter constant.
    /// </para>
    /// </remarks>
    public Guid? CurrentTenantId => this.tenantIdAccessor();

    /// <summary>
    /// Applies the contract-driven blueprint conventions to the completed model.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="BlueprintModelCustomizer"/> after <c>OnModelCreating</c> has run in full, so
    /// that entity types the application registers after its <c>base.OnModelCreating</c> call are covered
    /// too. Applying these inline in <c>OnModelCreating</c> silently skipped them.
    /// </remarks>
    /// <param name="modelBuilder">The model builder holding the completed model.</param>
    public void ApplyBlueprintConventions(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Pass 'this' so the tenant filter closes over a DbContext-typed constant. EF Core's
        // ParameterExtractingExpressionVisitor detects it and re-evaluates CurrentTenantId against the
        // executing context per query, rather than baking in the model-building instance.
        modelBuilder.ConfigureBlueprintMultiTenancy<TAppTenant, BlueprintDbContext<TAppTenant, TAppUser, TAppRole>>(this);
        modelBuilder.ConfigureContractConstraints();
        modelBuilder.ConfigureLocalizedTextConventions();
        modelBuilder.ConfigureEntityFiltering();

        // Last, so entity types introduced by the conventions above are covered as well.
        modelBuilder.ConfigureGuidPrimaryKeyDefaults();
    }

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types.
    /// </summary>
    /// <param name="builder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new BlueprintActiveSessionConfiguration());
        builder.ApplyConfiguration(new BlueprintAuditLogConfiguration());
        builder.ApplyConfiguration(new BlueprintResourceAccessConfiguration());
        builder.ApplyConfiguration(new BlueprintRoleConfiguration<TAppRole>());
        builder.ApplyConfiguration(new BlueprintServiceAccountConfiguration());
        builder.ApplyConfiguration(new BlueprintServiceAccountSessionConfiguration());
        builder.ApplyConfiguration(new BlueprintTableViewPreferenceConfiguration<TAppUser>());
        builder.ApplyConfiguration(new BlueprintTenantConfiguration<TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTenantMembershipConfiguration<TAppUser, TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTenantMembershipRoleConfiguration<TAppUser, TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTenantMembershipPermissionConfiguration<TAppUser, TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTranslationConfiguration());
        builder.ApplyConfiguration(new BlueprintUserConfiguration<TAppUser>());

        // The contract-driven conventions (multi-tenancy, contract constraints, localized texts,
        // soft-delete filtering, sequential Guid keys) are NOT applied here. They run at model
        // finalization via BlueprintModelFinalizingConvention, registered in ConfigureConventions,
        // so that entity types an application registers after calling base.OnModelCreating are
        // covered as well. See that convention for why.
    }

    /// <summary>
    /// Registers the model customizer that applies the blueprint conventions once the model is complete.
    /// </summary>
    /// <remarks>
    /// A derived context that overrides this method <b>must</b> call <c>base.OnConfiguring</c>, or the
    /// blueprint conventions — tenant isolation and soft-delete filtering among them — are never applied.
    /// </remarks>
    /// <param name="optionsBuilder">The options builder for this context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);

        optionsBuilder.ReplaceService<IModelCustomizer, BlueprintModelCustomizer>();
    }
}
