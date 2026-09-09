namespace Csag.Blueprint.TestHost.Extensions;

using Csag.Blueprint.Infrastructure.Localization;
using Csag.Blueprint.Infrastructure.TableView;
using Csag.Blueprint.TestHost.HostedServices;
using Csag.Blueprint.TestHost.Localization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Composes all host-level service registrations on top of the Blueprint package services:
/// persistence, identity, session authentication, authorization policies, table views,
/// database-backed localization, health checks, and the startup orchestration.
/// </summary>
public static class TestHostServicesExtensions
{
    /// <summary>
    /// Adds every host-specific service. Call after <c>AddBlueprintServices</c> so package-level
    /// registrations (antiforgery, distributed cache, FastEndpoints, options) are already in place.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder for chaining.</returns>
    public static WebApplicationBuilder AddTestHostServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var securitySettings = builder.Configuration.GetValidatedSecuritySettings();
        var localizationOptions = builder.Configuration.GetValidatedLocalizationOptions();

        builder.Services
            .AddTestHostPersistence(builder.Configuration)
            .AddTestHostIdentity(securitySettings)
            .AddTestHostSessionAuthentication(
                securitySettings.CookieSecurePolicy,
                TimeSpan.FromHours(securitySettings.SessionExpirationHours))
            .AddTestHostAuthorizationPolicies()
            .AddTestHostHealthChecks()
            .AddBlueprintTableView(typeof(TestHostServicesExtensions).Assembly)
            .AddBlueprintTableViewPreferences<TestDbContext, TestUser>()
            .AddBlueprintDbLocalization<TestDbContext>(
                localizationOptions.DefaultLanguage,
                TranslationDefaults.All,
                localizationOptions.TranslationCacheL1ExpirationMinutes);

        // This host runs as a single instance, so the distributed cache backing sessions and
        // translation snapshots is a distributed *memory* cache: same IDistributedCache contract the
        // ticket store and translation provider consume, without requiring the SQL cache table a
        // multi-node deployment would provision. Replaces the provider registered by the package's
        // cache wiring, which resolves the last IDistributedCache registration.
        builder.Services.RemoveAll<IDistributedCache>();
        builder.Services.AddDistributedMemoryCache();

        // Creates the schema, seeds the deterministic test data, and flips the readiness gate.
        builder.Services.AddScoped<Database.TestHostDataSeeder>();
        builder.Services.AddHostedService<TestHostStartupService>();

        return builder;
    }
}
