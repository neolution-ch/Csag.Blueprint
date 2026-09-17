namespace Csag.Blueprint.Testing.UnitTests.Unit;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unit tests for <see cref="TestDbContextScope{TDbContext}"/> verifying that it exposes the wrapped
/// context unchanged, disposes it exactly once, and treats repeated disposal as a no-op.
/// </summary>
public sealed class TestDbContextScopeTests : IDisposable
{
    private bool disposed;

    [Fact]
    public void Context_ReturnsTheExactInstancePassedToTheConstructor()
    {
        // Arrange
        using var context = new DisposeCountingDbContext(CreateInMemoryOptions());

        // Act
        using var scope = new TestDbContextScope<DisposeCountingDbContext>(context);

        // Assert — the scope is a plain wrapper; it must not proxy or replace the context.
        scope.Context.ShouldBeSameAs(context);
    }

    [Fact]
    public void Dispose_DisposesTheWrappedContext()
    {
        // Arrange
        var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var context = scope.Context;

        // Act
        scope.Dispose();

        // Assert — using the context after the scope is disposed must fail.
        Should.Throw<ObjectDisposedException>(() => context.Model);
    }

    [Fact]
    public void Dispose_CalledTwice_DisposesTheContextExactlyOnce()
    {
        // Arrange
        using var countingContext = new DisposeCountingDbContext(CreateInMemoryOptions());
        var scope = new TestDbContextScope<DisposeCountingDbContext>(countingContext);

        // Act — the second call is a no-op guarded by the internal disposed flag.
        Should.NotThrow(() => scope.Dispose());
        Should.NotThrow(() => scope.Dispose());

        // Assert
        countingContext.DisposeCount.ShouldBe(1);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // Reset the ambient tenant set by TestDbContextFactory so tests don't leak into one another.
        TenantContext.Clear();
        this.disposed = true;
    }

    private static DbContextOptions<DisposeCountingDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<DisposeCountingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    /// <summary>
    /// A minimal context that counts how many times <see cref="DbContext.Dispose()"/> is invoked,
    /// making the scope's exactly-once disposal contract directly observable.
    /// </summary>
    private sealed class DisposeCountingDbContext : DbContext
    {
        public DisposeCountingDbContext(DbContextOptions<DisposeCountingDbContext> options)
            : base(options)
        {
        }

        public int DisposeCount { get; private set; }

        public override void Dispose()
        {
            this.DisposeCount++;
            base.Dispose();
        }
    }
}
