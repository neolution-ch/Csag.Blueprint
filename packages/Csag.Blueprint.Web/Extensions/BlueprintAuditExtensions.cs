namespace Csag.Blueprint.Web.Extensions;

using System.Security.Claims;
using Audit.Core;
using Audit.EntityFramework.ConfigurationApi;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring blueprint audit logging on WebApplication.
/// </summary>
public static class BlueprintAuditExtensions
{
    /// <summary>
    /// Configures Audit.NET with the SQL Server data provider, EF Core entity tracking,
    /// and custom enrichment from HTTP context (user identity and correlation ID).
    /// Standard blueprint entity exclusions are applied automatically.
    /// Must be called after the application is built so that IServiceProvider is available.
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext type.</typeparam>
    /// <typeparam name="TUser">The application's user entity type.</typeparam>
    /// <typeparam name="TRole">The application's role entity type.</typeparam>
    /// <param name="app">The web application.</param>
    /// <param name="configure">Optional callback to configure app-specific audit settings.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication ConfigureBlueprintAuditLogging<TContext, TUser, TRole>(
        this WebApplication app,
        Action<BlueprintAuditOptions<TContext>>? configure = null)
        where TContext : DbContext
        where TUser : BlueprintUser
        where TRole : BlueprintRole
    {
        var options = new BlueprintAuditOptions<TContext>();
        configure?.Invoke(options);

        var connectionString = app.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("DefaultConnection not found");

        // Reset any previously registered custom actions to prevent stale closures
        // (important when the app host is recreated, e.g., during integration tests)
        Configuration.ResetCustomActions();

        ConfigureSqlServerDataProvider(connectionString);
        ConfigureEntityFrameworkAudit<TContext, TUser, TRole>(options);
        ConfigureHttpContextEnrichment(app.Services);

        return app;
    }

    private static void ConfigureSqlServerDataProvider(string connectionString)
    {
        Configuration.Setup()
            .UseSqlServer(config => config
                .ConnectionString(connectionString)
                .Schema("dbo")
                .TableName("BlueprintAuditLogs")
                .IdColumnName("Id")
                .JsonColumnName("JsonData")
                .CustomColumn("EventType", ev => ev.EventType?.Length > 100 ? ev.EventType[..100] : ev.EventType)
                .CustomColumn("CreatedAt", _ => DateTimeOffset.UtcNow)
                .CustomColumn("UserId", ev =>
                    ev.CustomFields.TryGetValue("UserId", out var val) ? val?.ToString() : null)
                .CustomColumn("CorrelationId", ev =>
                    ev.CustomFields.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var val) ? val?.ToString() : null));
    }

    private static void ConfigureEntityFrameworkAudit<TContext, TUser, TRole>(
        BlueprintAuditOptions<TContext> options)
        where TContext : DbContext
        where TUser : BlueprintUser
        where TRole : BlueprintRole
    {
        // OptOut mode: all entities are audited by default, except those explicitly ignored.
        // Standard blueprint entity field exclusions prevent sensitive data leakage into the audit log.
        var contextConfig = Audit.EntityFramework.Configuration.Setup()
            .ForContext<TContext>(config =>
            {
                config
                    .IncludeEntityObjects()
                    .AuditEventType("{context}:{database}")
                    .ForEntity<TUser>(entity => entity
                        .Ignore(u => u.PasswordHash)
                        .Ignore(u => u.SecurityStamp)
                        .Ignore(u => u.ConcurrencyStamp))
                    .ForEntity<BlueprintServiceAccount>(entity => entity
                        .Ignore(s => s.ClientSecretHash));

                // Allow app to add its own entity field exclusions
                options.EntityFieldConfigurator?.Invoke(config);
            });

        contextConfig.UseOptOut()

            // Infrastructure: session tracking, high-frequency writes, no business audit value
            .Ignore<BlueprintActiveSession>()

            // Infrastructure: data protection key management, rotated automatically
            .Ignore<DataProtectionKey>()

            // Infrastructure: audit log itself must not be audited to prevent infinite recursion
            .Ignore<BlueprintAuditLog>()

            // ASP.NET Identity framework entities: managed by Identity, not business entities
            .Ignore<TRole>()
            .Ignore<IdentityRoleClaim<Guid>>()
            .Ignore<IdentityUserClaim<Guid>>()
            .Ignore<IdentityUserLogin<Guid>>()
            .Ignore<IdentityUserRole<Guid>>()
            .Ignore<IdentityUserToken<Guid>>();
    }

    /// <summary>
    /// Adds a custom action on every audit scope to enrich events with the current HTTP context:
    /// the authenticated user ID (from JWT/cookie claims) and the correlation ID
    /// (set by <see cref="CorrelationIdMiddleware"/> earlier in the pipeline).
    /// This is what links EF-level entity change events to the specific user and request that caused them.
    /// </summary>
    private static void ConfigureHttpContextEnrichment(IServiceProvider serviceProvider)
    {
        // Resolve IHttpContextAccessor eagerly — it is a singleton that uses AsyncLocal internally,
        // so capturing it here is safe and avoids accessing a disposed IServiceProvider later.
        var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();

        Configuration.AddCustomAction(ActionType.OnScopeCreated, scope =>
        {
            var httpContext = httpContextAccessor?.HttpContext;

            if (httpContext is null)
            {
                return;
            }

            var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is not null)
            {
                scope.SetCustomField("UserId", userId);
            }

            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var correlationId)
                && correlationId is not null)
            {
                scope.SetCustomField(CorrelationIdMiddleware.CorrelationIdKey, correlationId.ToString());
            }
        });
    }
}
