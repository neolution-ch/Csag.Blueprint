namespace Csag.Blueprint.Infrastructure.Session;

using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IActiveSessionTracker"/> that writes the tracked session row through
/// the pooled context factory, so it is safe to consume from singletons such as the ticket store.
/// </summary>
/// <typeparam name="TContext">The application database context type.</typeparam>
public sealed class ActiveSessionTracker<TContext> : IActiveSessionTracker
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveSessionTracker{TContext}"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory used to write active session records.</param>
    public ActiveSessionTracker(IDbContextFactory<TContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <inheritdoc/>
    public async Task<bool> ExtendAsync(string sessionKey, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rowsAffected = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.ExpiresAt, expiresUtc), cancellationToken);

        return rowsAffected > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UntrackAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rowsAffected = await dbContext.Set<BlueprintActiveSession>()
            .Where(s => s.SessionKey == sessionKey)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }
}
