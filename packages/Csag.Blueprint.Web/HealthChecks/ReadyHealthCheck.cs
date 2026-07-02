namespace Csag.Blueprint.Web.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check that verifies if the application startup has completed.
/// </summary>
public class ReadyHealthCheck : IHealthCheck
{
    private volatile bool isReady;

    /// <summary>
    /// Gets or sets a value indicating whether startup has completed.
    /// </summary>
    public bool StartupCompleted
    {
        get => this.isReady;
        set => this.isReady = value;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (this.StartupCompleted)
        {
            return Task.FromResult(HealthCheckResult.Healthy("The startup task has completed."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("The startup task is still running."));
    }
}
