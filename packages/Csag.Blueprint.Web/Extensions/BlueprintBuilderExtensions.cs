namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Aggregate extension methods that compose individual Blueprint builder extensions
/// into a single call for cleaner Program.cs setup.
/// </summary>
public static class BlueprintBuilderExtensions
{
    /// <summary>
    /// Configures all Blueprint-level services: server options (HTTPS, HSTS, CORS, Kestrel, request size limits),
    /// generic OpenID Connect external authentication, FastEndpoints, Swagger, distributed cache,
    /// session authentication, and anti-forgery protection.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder for chaining.</returns>
    public static WebApplicationBuilder AddBlueprintServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var securitySettings = builder.Configuration.GetValidatedSecuritySettings();
        var cacheOptions = builder.Configuration.GetValidatedCacheOptions();

        builder
            .AddHttpsRedirection(securitySettings)
            .AddHsts(securitySettings)
            .AddConfiguredCors(securitySettings)
            .ConfigureKestrelServerOptions(securitySettings)
            .ConfigureFormOptions(securitySettings);

        builder.Services
            .AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = ctx =>
                {
                    // In Development, include the exception message and stack trace in the
                    // Problem Details response so developers get actionable error info.
                    // In Production this is skipped — only the generic title and status are returned.
                    var env = ctx.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                    if (!env.IsDevelopment())
                    {
                        return;
                    }

                    var exceptionFeature = ctx.HttpContext.Features.Get<IExceptionHandlerFeature>();
                    if (exceptionFeature?.Error is not null)
                    {
                        ctx.ProblemDetails.Detail = exceptionFeature.Error.Message;
                        ctx.ProblemDetails.Extensions["exception"] = exceptionFeature.Error.ToString();
                    }
                };
            })
            .AddOidcAuthentication(securitySettings)
            .AddFastEndpointsWithConfiguration()
            .AddSwaggerDocumentation()
            .AddConfigurableDistributedCache(cacheOptions, builder.Configuration)
            .AddAntiForgeryProtection(securitySettings);

        // Addressing seam: how a request declares which tenant it belongs to. TryAdd, so an
        // ITenantResolver registered before this call wins over the claims-based default.
        builder.Services.TryAddScoped<ITenantResolver, ClaimsTenantResolver>();

        return builder;
    }
}
