namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Result of a tenant lifecycle operation.
/// </summary>
public sealed class TenantOperationResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the error message when the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="TenantOperationResult"/>.</returns>
    public static TenantOperationResult Success() => new() { Succeeded = true };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A failed <see cref="TenantOperationResult"/>.</returns>
    public static TenantOperationResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}
