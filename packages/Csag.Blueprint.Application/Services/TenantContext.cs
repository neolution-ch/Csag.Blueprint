namespace Csag.Blueprint.Application.Services;

using System;
using System.Threading;

/// <summary>
/// Provides ambient context for the current tenant ID using AsyncLocal for async/await safety.
/// This context flows naturally through asynchronous operations and is isolated per execution context.
/// Used by query filters, save interceptors, and middleware to maintain tenant isolation.
/// </summary>
public static class TenantContext
{
    private static readonly AsyncLocal<Guid?> CurrentTenantIdStorage = new();

    /// <summary>
    /// Gets the current tenant ID for this execution context.
    /// Returns null if no tenant context has been set.
    /// </summary>
    public static Guid? Current => CurrentTenantIdStorage.Value;

    /// <summary>
    /// Sets the current tenant ID for this execution context.
    /// This value flows through async/await calls but is isolated per request/operation.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set as current.</param>
    public static void SetTenant(Guid tenantId)
    {
        CurrentTenantIdStorage.Value = tenantId;
    }

    /// <summary>
    /// Clears the current tenant context.
    /// Useful for cleanup or switching to a non-tenant context.
    /// </summary>
    public static void Clear()
    {
        CurrentTenantIdStorage.Value = null;
    }
}
