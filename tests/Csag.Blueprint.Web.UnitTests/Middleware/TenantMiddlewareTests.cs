namespace Csag.Blueprint.Web.UnitTests.Middleware;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Web.Middleware;
using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="TenantMiddleware"/>, covering the two resolver outcomes (tenant resolved →
/// stamped into <see cref="TenantContext"/> for the downstream pipeline; not resolved → ambient context
/// explicitly cleared for the downstream pipeline) and the per-request clear-in-<c>finally</c>
/// lifecycle, including the failure path. The stamped tenant is observed by reading
/// <see cref="TenantContext.Current"/> inside the terminal delegate, because the middleware clears it
/// again before returning.
/// </summary>
public sealed class TenantMiddlewareTests : IDisposable
{
    private bool disposed;
    private Guid? tenantSeenByNext;

    public TenantMiddlewareTests()
    {
        // The tenant context is a static AsyncLocal; make sure no prior test leaked a value into it.
        TenantContext.Clear();
    }

    [Fact]
    public async Task InvokeAsync_TenantResolved_StampsTenantDuringDownstreamExecutionAsync()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();

        // Act
        await this.CreateMiddleware().InvokeAsync(context, new FakeTenantResolver(tenantId));

        // Assert
        this.tenantSeenByNext.ShouldBe(tenantId);
    }

    [Fact]
    public async Task InvokeAsync_TenantNotResolved_LeavesAmbientTenantNullAsync()
    {
        // Arrange — "no tenant context" is a normal state (anonymous request, platform-scope user),
        // and the query filters downstream fail closed on a null ambient tenant.
        var context = new DefaultHttpContext();

        // Act
        await this.CreateMiddleware().InvokeAsync(context, new FakeTenantResolver(tenantId: null));

        // Assert
        this.tenantSeenByNext.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_TenantNotResolved_ClearsPreexistingAmbientTenantAsync()
    {
        // Arrange — an ambient value stamped earlier in the calling execution context (e.g. an
        // in-process caller) must never masquerade as the request's tenant: when the resolver
        // yields nothing, the middleware clears the context before invoking downstream so the
        // query filters fail closed.
        var preexisting = Guid.NewGuid();
        TenantContext.SetTenant(preexisting);
        var context = new DefaultHttpContext();

        // Act
        await this.CreateMiddleware().InvokeAsync(context, new FakeTenantResolver(tenantId: null));

        // Assert — downstream saw no tenant. The caller still sees its own value afterwards:
        // AsyncLocal writes inside the awaited middleware (including the clear) affect only the
        // middleware's execution context and everything downstream of it, never the caller's.
        this.tenantSeenByNext.ShouldBeNull();
        TenantContext.Current.ShouldBe(preexisting);
    }

    [Fact]
    public async Task InvokeAsync_ClearsAmbientTenantAfterRequestAsync()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();

        // Act
        await this.CreateMiddleware().InvokeAsync(context, new FakeTenantResolver(tenantId));

        // Assert — the tenant was set during the request but cleared once it completed.
        this.tenantSeenByNext.ShouldBe(tenantId);
        TenantContext.Current.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_DownstreamThrows_ClearsAmbientTenantAndPropagatesAsync()
    {
        // Arrange — the clear must run even on failure, because the AsyncLocal-backed context would
        // otherwise leak the tenant into whatever continues on this execution context.
        var middleware = new TenantMiddleware(_ => throw new InvalidOperationException("downstream failure"));
        var context = new DefaultHttpContext();

        // Act + Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await middleware.InvokeAsync(context, new FakeTenantResolver(Guid.NewGuid())));

        TenantContext.Current.ShouldBeNull();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        TenantContext.Clear();
        this.disposed = true;
    }

    private TenantMiddleware CreateMiddleware() => new(context =>
    {
        // Capture what the query filters would see mid-request, before the middleware clears it.
        this.tenantSeenByNext = TenantContext.Current;
        return Task.CompletedTask;
    });

    private sealed class FakeTenantResolver : ITenantResolver
    {
        private readonly Guid? tenantId;

        public FakeTenantResolver(Guid? tenantId)
        {
            this.tenantId = tenantId;
        }

        public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.tenantId);
    }
}
