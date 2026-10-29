namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Infrastructure.ServiceAccounts;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Web.Options;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Cache;
using Csag.Blueprint.Web.Options.Database;
using Csag.Blueprint.Web.Options.FeatureFlags;
using Csag.Blueprint.Web.Options.Frontend;
using Csag.Blueprint.Web.Options.Localization;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for configuring options with FluentValidation.
/// </summary>
public static class OptionsExtensions
{
    /// <summary>
    /// Adds options with FluentValidation support.
    /// Configures the options to bind from the specified configuration section and validates on application start.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidator<TOptions>, TValidator>();
        services.AddSingleton<IValidateOptions<TOptions>, FluentValidationOptions<TOptions>>();
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds all blueprint default options with FluentValidation support.
    /// Registers DatabaseOptions, SecuritySettings, FeatureFlagOptions, CacheOptions, LocalizationOptions, and FrontendSettings
    /// with their respective validators, all nested under the "Blueprint" configuration section. Derives the
    /// session lifetime (<see cref="BlueprintSessionOptions.Lifetime"/>) from <see cref="SecuritySettings.SessionExpirationHours"/>,
    /// and the service-account settings (<see cref="BlueprintServiceAccountOptions"/>) from
    /// <see cref="SecuritySettings.Jwt"/> and <see cref="SecuritySettings.ServiceAccountLockout"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintDefaultValidatedOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<DatabaseOptions, DatabaseOptionsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:Database");

        services.AddValidatedOptions<SecuritySettings, SecuritySettingsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:Security");

        // SessionExpirationHours is the single source of the session lifetime. A post-configure step applies it
        // after any Configure an application registers for the session options, so the two cannot disagree.
        services.AddOptions<BlueprintSessionOptions>()
            .PostConfigure<IOptions<SecuritySettings>>((session, security) =>
                session.Lifetime = TimeSpan.FromHours(security.Value.SessionExpirationHours));

        // The same for service-account credentials: Blueprint:Security:Jwt and :ServiceAccountLockout are their single
        // source, applied after any Configure the application registers.
        services.AddOptions<BlueprintServiceAccountOptions>()
            .PostConfigure<IOptions<SecuritySettings>>((serviceAccounts, security) =>
            {
                var jwt = security.Value.Jwt;
                var lockout = security.Value.ServiceAccountLockout;
                serviceAccounts.SigningKey = jwt.SigningKey;
                serviceAccounts.Issuer = jwt.Issuer;
                serviceAccounts.Audience = jwt.Audience;
                serviceAccounts.TokenLifetime = TimeSpan.FromHours(jwt.ExpirationHours);
                serviceAccounts.MaxFailedAccessAttempts = lockout.MaxFailedAccessAttempts;
                serviceAccounts.LockoutDuration = TimeSpan.FromMinutes(lockout.LockoutDurationMinutes);
            });

        services.AddValidatedOptions<FeatureFlagOptions, FeatureFlagOptionsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:FeatureFlags");

        services.AddValidatedOptions<CacheOptions, CacheOptionsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:Cache");

        services.AddValidatedOptions<LocalizationOptions, LocalizationOptionsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:Localization");

        services.AddValidatedOptions<FrontendSettings, FrontendSettingsValidator>(
            configuration,
            $"{BlueprintOptions.SectionName}:Frontend");

        return services;
    }
}
