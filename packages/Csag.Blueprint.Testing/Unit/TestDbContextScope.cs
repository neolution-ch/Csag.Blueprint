namespace Csag.Blueprint.Testing.Unit;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Disposable wrapper that manages a <typeparamref name="TDbContext"/> instance lifecycle.
/// Ensures proper cleanup of resources when disposed.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public sealed class TestDbContextScope<TDbContext> : IDisposable
    where TDbContext : DbContext
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDbContextScope{TDbContext}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public TestDbContextScope(TDbContext context)
    {
        this.Context = context;
    }

    /// <summary>
    /// Gets the database context instance.
    /// </summary>
    public TDbContext Context { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Context.Dispose();
        this.disposed = true;
    }
}
