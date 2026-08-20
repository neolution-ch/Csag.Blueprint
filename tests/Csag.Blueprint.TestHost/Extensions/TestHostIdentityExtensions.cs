namespace Csag.Blueprint.TestHost.Extensions;

using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Registers ASP.NET Core Identity with the shared test entity closures and the Blueprint
/// principal builder used by the login endpoint.
/// </summary>
public static class TestHostIdentityExtensions
{
    /// <summary>
    /// Adds Identity with Entity Framework stores over <see cref="TestDbContext"/>, applying the
    /// configured password policy, and registers the <see cref="AuthenticatedPrincipalBuilder{TUser}"/>
    /// so sign-in flows produce principals with role and permission claims already applied.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="securitySettings">The validated security settings containing the password policy.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostIdentity(this IServiceCollection services, SecuritySettings securitySettings)
    {
        ArgumentNullException.ThrowIfNull(securitySettings);

        services.AddIdentity<TestUser, TestRole>(options =>
        {
            var passwordSettings = securitySettings.PasswordSettings;
            options.Password.RequireDigit = passwordSettings.RequireDigit;
            options.Password.RequireLowercase = passwordSettings.RequireLowercase;
            options.Password.RequireNonAlphanumeric = passwordSettings.RequireNonAlphanumeric;
            options.Password.RequireUppercase = passwordSettings.RequireUppercase;
            options.Password.RequiredLength = passwordSettings.RequiredLength;
            options.Password.RequiredUniqueChars = passwordSettings.RequiredUniqueChars;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<TestDbContext>()
        .AddDefaultTokenProviders();

        // The principal builder composes profile, tenant, role, and permission claims for sign-in.
        // Registered next to the UserManager it depends on.
        services.AddScoped<IAuthenticatedPrincipalBuilder<TestUser>, AuthenticatedPrincipalBuilder<TestUser>>();

        return services;
    }
}
