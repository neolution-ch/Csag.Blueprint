namespace Csag.Blueprint.Tests.Shared.Session;

using System.Reflection;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Reaches the package-internal service-account session manager for the tests that pin its session-key handling,
/// its revocation sweeps and its account reads. The package declares no <c>InternalsVisibleTo</c>, so the manager is
/// resolved and called through reflection. Every call is forwarded with <see cref="BindingFlags.DoNotWrapExceptions"/>,
/// so an exception the manager throws before returning its task still surfaces at the call site, unwrapped.
/// </summary>
public sealed class ServiceAccountSessionManagerAccessor
{
    private static readonly Assembly InfrastructureAssembly = typeof(ExpiredSessionCleanupService<>).Assembly;

    private static readonly Type ManagerInterface = InfrastructureAssembly
        .GetType("Csag.Blueprint.Infrastructure.Session.IServiceAccountSessionManager", throwOnError: true)!;

    private static readonly Type ManagerType = InfrastructureAssembly
        .GetType("Csag.Blueprint.Infrastructure.Session.ServiceAccountSessionManager`1", throwOnError: true)!
        .MakeGenericType(typeof(TestDbContext));

    private readonly object manager;

    private ServiceAccountSessionManagerAccessor(object manager)
    {
        this.manager = manager;
    }

    /// <summary>
    /// Resolves the manager the package registered, as the package's own components receive it.
    /// </summary>
    /// <param name="services">A scope's service provider.</param>
    /// <returns>The accessor.</returns>
    public static ServiceAccountSessionManagerAccessor Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new ServiceAccountSessionManagerAccessor(services.GetRequiredService(ManagerInterface));
    }

    /// <summary>
    /// Builds a manager over the given collaborators, for a test that substitutes the cache or the clock.
    /// </summary>
    /// <param name="dbContextFactory">The context factory the manager reads and writes through.</param>
    /// <param name="cache">The distributed cache holding the markers.</param>
    /// <param name="timeProvider">The clock the manager stamps and compares with.</param>
    /// <returns>The accessor.</returns>
    public static ServiceAccountSessionManagerAccessor Create(
        IDbContextFactory<TestDbContext> dbContextFactory,
        IDistributedCache<CacheId> cache,
        TimeProvider timeProvider)
    {
        var instance = Activator.CreateInstance(
            ManagerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DoNotWrapExceptions,
            binder: null,
            [dbContextFactory, cache, timeProvider],
            culture: null)!;

        return new ServiceAccountSessionManagerAccessor(instance);
    }

    /// <summary>
    /// Forwards to <c>TrackSessionAsync</c>.
    /// </summary>
    /// <param name="serviceAccountId">The account the session belongs to.</param>
    /// <param name="sessionKey">The session key.</param>
    /// <param name="expiresAt">The session's expiry.</param>
    /// <param name="userAgent">The recorded user agent.</param>
    /// <param name="ipAddress">The recorded address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manager's task.</returns>
    public Task TrackSessionAsync(
        Guid serviceAccountId,
        string sessionKey,
        DateTimeOffset expiresAt,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
        => (Task)this.Invoke("TrackSessionAsync", serviceAccountId, sessionKey, expiresAt, userAgent, ipAddress, cancellationToken);

    /// <summary>
    /// Forwards to <c>ValidateSessionAsync</c> and copies the resolved session out of the internal result type.
    /// </summary>
    /// <param name="sessionKey">The session key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved session, or <see langword="null"/>.</returns>
    public async Task<ServiceAccountSessionSnapshot?> ValidateSessionAsync(string? sessionKey, CancellationToken cancellationToken = default)
    {
        var task = (Task)this.Invoke("ValidateSessionAsync", sessionKey, cancellationToken);
        await task;

        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        return result is null ? null : ServiceAccountSessionSnapshot.From(result);
    }

    /// <summary>
    /// Forwards to <c>RevokeSessionAsync</c>.
    /// </summary>
    /// <param name="sessionKey">The session key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manager's task.</returns>
    public Task<bool> RevokeSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
        => (Task<bool>)this.Invoke("RevokeSessionAsync", sessionKey, cancellationToken);

    /// <summary>
    /// Forwards to <c>RevokeServiceAccountSessionsAsync</c>.
    /// </summary>
    /// <param name="serviceAccountId">The account whose sessions are revoked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manager's task.</returns>
    public Task<int> RevokeServiceAccountSessionsAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
        => (Task<int>)this.Invoke("RevokeServiceAccountSessionsAsync", serviceAccountId, cancellationToken);

    private object Invoke(string methodName, params object?[] arguments)
    {
        var method = ManagerInterface.GetMethod(methodName)
            ?? throw new MissingMethodException(ManagerInterface.FullName, methodName);

        return method.Invoke(this.manager, BindingFlags.DoNotWrapExceptions, binder: null, arguments, culture: null)!;
    }
}
