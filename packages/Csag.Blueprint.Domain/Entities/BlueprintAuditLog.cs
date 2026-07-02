namespace Csag.Blueprint.Domain.Entities;

/// <summary>
/// Represents a stored audit event from Audit.NET.
/// Audit events are immutable; only <see cref="CreatedAt"/> is captured.
/// </summary>
public sealed class BlueprintAuditLog
{
    /// <summary>
    /// Gets or sets the unique identifier for the audit log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the type of audit event (e.g., entity type or HTTP method/path).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the audit event was created.
    /// This is immutable for audit log entries.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the full audit event data serialized as JSON.
    /// </summary>
    public string JsonData { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID associated with the audit event.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for request tracing.
    /// </summary>
    public string? CorrelationId { get; set; }
}
