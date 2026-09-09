namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Security.Claims;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="CurrentActorMiddleware"/>, covering the actor-resolution branches:
/// the email-preferred path, the <c>type == "ServiceAccount"</c> gate with its
/// <c>sub</c> → <see cref="ClaimTypes.NameIdentifier"/> fallback, the not-resolvable / unauthenticated
/// paths that leave the ambient context null, and the per-request set/clear lifecycle.
/// The middleware is driven through its public <see cref="CurrentActorMiddleware.InvokeAsync"/>; the
/// resolved actor is observed by reading <see cref="CurrentActorContext.Current"/> inside the terminal
/// delegate (the middleware clears it again in its <c>finally</c>).
/// </summary>
public sealed class CurrentActorMiddlewareTests : IDisposable
{
    private bool disposed;
    private string? actorSeenByNext;

    public CurrentActorMiddlewareTests()
    {
        // The actor context is a static AsyncLocal; make sure no prior test leaked a value into it.
        CurrentActorContext.Clear();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithEmail_StampsEmail()
    {
        // Arrange
        var context = ContextFor(Authenticated(new Claim(ClaimTypes.Email, "alice@example.com")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithSub_StampsSub()
    {
        // Arrange
        var context = ContextFor(Authenticated(
            new Claim("type", "ServiceAccount"),
            new Claim("sub", "sa-abc123")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBe("sa-abc123");
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithoutSub_FallsBackToNameIdentifier()
    {
        // Arrange — JWT inbound-claim mapping may have renamed "sub" to NameIdentifier.
        var context = ContextFor(Authenticated(
            new Claim("type", "ServiceAccount"),
            new Claim(ClaimTypes.NameIdentifier, "sa-xyz789")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBe("sa-xyz789");
    }

    [Fact]
    public async Task InvokeAsync_EmailPresentAndServiceAccount_PrefersEmail()
    {
        // Arrange — email always wins over the service-account branch.
        var context = ContextFor(Authenticated(
            new Claim(ClaimTypes.Email, "human@example.com"),
            new Claim("type", "ServiceAccount"),
            new Claim("sub", "sa-abc123")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBe("human@example.com");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithNoEmailAndNotServiceAccount_LeavesActorNull()
    {
        // Arrange — authenticated but nothing resolvable (no email, not a service account).
        var context = ContextFor(Authenticated(new Claim(ClaimTypes.NameIdentifier, "some-user-id")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithoutSubOrNameIdentifier_LeavesActorNull()
    {
        // Arrange — service account whose id claims are both absent resolves to nothing.
        var context = ContextFor(Authenticated(new Claim("type", "ServiceAccount")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_LeavesActorNull()
    {
        // Arrange — an identity with no authentication type is not authenticated, so the email claim
        // must be ignored and the context left unset.
        var context = ContextFor(Unauthenticated(new Claim(ClaimTypes.Email, "ignored@example.com")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert
        this.actorSeenByNext.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_ClearsAmbientContextAfterRequest()
    {
        // Arrange
        var context = ContextFor(Authenticated(new Claim(ClaimTypes.Email, "alice@example.com")));

        // Act
        await this.CreateMiddleware().InvokeAsync(context);

        // Assert — the actor was set during the request but cleared once it completed.
        this.actorSeenByNext.ShouldBe("alice@example.com");
        CurrentActorContext.Current.ShouldBeNull();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        CurrentActorContext.Clear();
        this.disposed = true;
    }

    private static DefaultHttpContext ContextFor(ClaimsPrincipal user) => new() { User = user };

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

    private static ClaimsPrincipal Unauthenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims));

    private CurrentActorMiddleware CreateMiddleware() => new(context =>
    {
        // Capture what the interceptor would see mid-request, before the middleware clears it.
        this.actorSeenByNext = CurrentActorContext.Current;
        return Task.CompletedTask;
    });
}
