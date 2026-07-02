namespace Csag.Blueprint.Web.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using NLog.Web;

/// <summary>
/// Extension methods for configuring logging on WebApplicationBuilder.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures logging with Application Insights telemetry and NLog.
    /// Clears default providers and sets up NLog as the primary logging provider.
    /// </summary>
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddApplicationInsightsTelemetry();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        // Disable Application Insights logger provider - let NLog handle all logging
        builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.None);
        builder.Host.UseNLog();

        return builder;
    }
}
