namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Registers service-account credentials and bearer authentication.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds service-account credentials and bearer authentication: <see cref="IServiceAccountCredentials"/> and
    /// <see cref="IServiceAccountSessionLifecycle"/>, the service-account sessions behind every token, and three
    /// authentication schemes — <see cref="BlueprintAuthenticationSchemes.ServiceAccountToken"/>, which validates
    /// service-account tokens; <see cref="BlueprintAuthenticationSchemes.Bearer"/>, which forwards each bearer request
    /// to the handler of its token kind; and <see cref="BlueprintAuthenticationSchemes.SessionOrBearer"/>, which
    /// forwards each request to the bearer selector when it carries a bearer token and to the session cookie
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registration needs no signing key, so it also serves a host started only to export its OpenAPI document:
    /// every service resolves, and the key is required the first time a token is issued or validated. A key that is
    /// configured is checked at startup.
    /// </para>
    /// <para>
    /// The service-account scheme pins HMAC-SHA256, validates issuer, audience and lifetime against the injected
    /// <see cref="TimeProvider"/> with no clock skew, does not map inbound claims, and resolves every token through its
    /// session, so revocation, rotation, deactivation and role changes apply on the next request. Register
    /// <see cref="IServiceAccountClaimsEnricher"/>s to add claims per request, and <see cref="ISessionRevocationParticipant"/>s
    /// to end the application's own credentials with the account's sessions. Add further token kinds behind the bearer
    /// selector through <see cref="BlueprintBearerOptions"/>.
    /// </para>
    /// <para>
    /// Expired service-account sessions are deleted by the job <c>AddBlueprintSessionInfrastructure</c> registers.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The application database context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the settings. <c>Csag.Blueprint.Web</c> sets them from
    /// <c>Blueprint:Security:Jwt</c> and <c>Blueprint:Security:ServiceAccountLockout</c> after this runs.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlueprintServiceAccountAuthentication<TContext>(
        this IServiceCollection services,
        Action<BlueprintServiceAccountOptions>? configure = null)
        where TContext : DbContext
    {
        // Clock seam for issuance, lockout and token lifetimes. TryAdd, so a TimeProvider registered before this call
        // (a FakeTimeProvider in tests) wins.
        services.TryAddSingleton(TimeProvider.System);

        // Issuance records the requesting client's user agent and address on the session.
        services.AddHttpContextAccessor();

        var serviceAccountOptions = services.AddOptions<BlueprintServiceAccountOptions>();
        if (configure is not null)
        {
            serviceAccountOptions.Configure(configure);
        }

        serviceAccountOptions.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BlueprintServiceAccountOptions>, BlueprintServiceAccountOptionsValidator>());

        services.TryAddScoped<IServiceAccountSessionManager, ServiceAccountSessionManager<TContext>>();
        services.TryAddSingleton<ServiceAccountTokenSigning>();
        services.TryAddSingleton<ServiceAccountCaller>();
        services.TryAddScoped<ServiceAccountSessionLifecycle>();
        services.TryAddScoped<IServiceAccountSessionLifecycle>(sp => sp.GetRequiredService<ServiceAccountSessionLifecycle>());
        services.TryAddScoped<IServiceAccountCredentials, ServiceAccountCredentials<TContext>>();

        services.AddAuthentication()
            .AddJwtBearer(BlueprintAuthenticationSchemes.ServiceAccountToken, _ => { })
            .AddPolicyScheme(
                BlueprintAuthenticationSchemes.Bearer,
                displayName: null,
                options => options.ForwardDefaultSelector = AuthenticationSchemeSelectors.SelectTokenKind)
            .AddPolicyScheme(
                BlueprintAuthenticationSchemes.SessionOrBearer,
                displayName: null,
                options => options.ForwardDefaultSelector = AuthenticationSchemeSelectors.SelectSessionOrBearer);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, ServiceAccountBearerOptionsSetup>());

        return services;
    }
}
