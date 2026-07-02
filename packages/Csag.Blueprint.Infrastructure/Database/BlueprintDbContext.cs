namespace Csag.Blueprint.Infrastructure.Database;

using System.Diagnostics.CodeAnalysis;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Database.Configurations;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Blueprint base database context for Identity, multi-tenancy, and shared business entities.
/// Multi-tenancy is handled through TenantContext (ambient AsyncLocal) which is set by middleware.
/// </summary>
/// <typeparam name="TAppTenant">The concrete tenant entity type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
/// <typeparam name="TAppUser">The concrete user entity type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TAppRole">The concrete role entity type, must derive from <see cref="BlueprintRole"/>.</typeparam>
[SuppressMessage("SonarQube", "S1200", Justification = "A DbContext necessarily aggregates all entity type dependencies.")]
[SuppressMessage("SonarQube", "S2436", Justification = "Three generic parameters are required to support tenant, user, and role entity customization.")]
public class BlueprintDbContext<TAppTenant, TAppUser, TAppRole> : IdentityDbContext<TAppUser, TAppRole, Guid>, IDataProtectionKeyContext
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
        builder.ApplyConfiguration(new BlueprintTableViewPreferenceConfiguration<TAppUser>());
        builder.ApplyConfiguration(new BlueprintTenantConfiguration<TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTenantMembershipConfiguration<TAppUser, TAppTenant>());
        builder.ApplyConfiguration(new BlueprintTranslationConfiguration());
        builder.ApplyConfiguration(new BlueprintUserConfiguration<TAppUser>());

        // Configure multi-tenancy filters and indexes.
        // Pass 'this' so the extension method builds expressions of the form:
        //   Expression.Property(Expression.Constant(this, typeof(BlueprintDbContext<...>)), "CurrentTenantId")
        // EF Core's ParameterExtractingExpressionVisitor detects the DbContext-typed constant and
        // re-evaluates the property against the current executing context per query,
        // not the model-building instance captured at startup.
        builder.ConfigureBlueprintMultiTenancy<TAppTenant, BlueprintDbContext<TAppTenant, TAppUser, TAppRole>>(this);
    }
}
