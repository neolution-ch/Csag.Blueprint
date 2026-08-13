namespace Csag.Blueprint.Application.Services;

using System.Threading;

/// <summary>
/// Provides ambient context for the current (acting) actor label using AsyncLocal for async/await safety.
/// This context flows naturally through asynchronous operations and is isolated per execution context.
/// Used by the audit save interceptor to stamp <c>CreatedByActor</c> / <c>UpdatedByActor</c> on auditable entities.
/// <para>
/// The actor label is a readable, point-in-time snapshot: the user's email for authenticated users,
/// the <c>sa-{clientId}</c> for service accounts, or null when there is no acting actor.
/// </para>
/// <para>
/// Mirrors <see cref="TenantContext"/>: an AsyncLocal-backed ambient value that is safe to read from a
/// singleton save-changes interceptor without capturing a scoped dependency.
/// </para>
/// </summary>
public static class CurrentActorContext
{
    private static readonly AsyncLocal<string?> CurrentActorStorage = new();

    /// <summary>
    /// Gets the current actor label for this execution context — the user's email, a service account's
    /// <c>sa-{clientId}</c>, or null when no actor has been set (e.g. data seeding, background services,
    /// migrations, or unauthenticated requests).
    /// </summary>
    public static string? Current => CurrentActorStorage.Value;

    /// <summary>
    /// Sets the current actor label for this execution context.
    /// This value flows through async/await calls but is isolated per request/operation.
    /// </summary>
    /// <param name="actor">The actor label to set as current (a user email or <c>sa-{clientId}</c>).</param>
    public static void SetActor(string actor)
    {
        CurrentActorStorage.Value = actor;
    }

    /// <summary>
    /// Clears the current actor context.
    /// Useful for cleanup or switching to a non-user (system) context.
    /// </summary>
    public static void Clear()
    {
        CurrentActorStorage.Value = null;
    }
}
