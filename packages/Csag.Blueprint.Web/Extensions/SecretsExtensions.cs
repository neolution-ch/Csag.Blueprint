namespace Csag.Blueprint.Web.Extensions;

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Neolution.Extensions.Configuration.GoogleSecrets;
using NLog;

/// <summary>
/// Extension methods for configuring Google Cloud Secret Manager on WebApplicationBuilder.
/// </summary>
public static class SecretsExtensions
{
    /// <summary>
    /// Configures Google Cloud Secret Manager integration if a project ID is available.
    /// Checks for project ID in Blueprint:GoogleSecrets:ProjectId configuration.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="logger">Optional NLog logger for diagnostic messages.</param>
    /// <returns>The WebApplicationBuilder for method chaining.</returns>
    public static WebApplicationBuilder AddGoogleSecretsIfConfigured(this WebApplicationBuilder builder, Logger? logger = null)
    {
        var projectId = builder.Configuration["Blueprint:GoogleSecrets:ProjectId"];

        if (!string.IsNullOrEmpty(projectId))
        {
            builder.Configuration.AddGoogleSecrets(options =>
            {
                options.ProjectName = projectId;
                options.MapFn = null;
            });
            logger?.Info(CultureInfo.InvariantCulture, "Google Secrets configured for project: {ProjectId}", projectId);
        }
        else
        {
            logger?.Info(CultureInfo.InvariantCulture, "Google Cloud Project ID not configured. Skipping Google Secrets.", Array.Empty<object>());
        }

        return builder;
    }
}
