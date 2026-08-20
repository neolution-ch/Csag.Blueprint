namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Net;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Unit tests for <see cref="OperationCancelledMiddleware"/>.
/// </summary>
public sealed class OperationCancelledMiddlewareTests
{
    private const int StatusCodeClientClosedRequest = 499;

    private readonly Mock<ILogger<OperationCancelledMiddleware>> loggerMock = new();

    [Fact]
    public async Task InvokeAsync_NormalRequest_PassesThroughWithoutIntervention()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new OperationCancelledMiddleware(
            _ => Task.CompletedTask,
            this.loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvokeAsync_ClientCancellation_Returns499()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };

        var middleware = new OperationCancelledMiddleware(
            _ => throw new OperationCanceledException(cts.Token),
            this.loggerMock.Object);

        // Simulate client abort
        await cts.CancelAsync();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodeClientClosedRequest);
    }

    [Fact]
    public async Task InvokeAsync_ClientCancellation_WithTaskCanceledException_Returns499()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };

        var middleware = new OperationCancelledMiddleware(
            _ => throw new TaskCanceledException("The operation was canceled.", null, cts.Token),
            this.loggerMock.Object);

        // Simulate client abort
        await cts.CancelAsync();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodeClientClosedRequest);
    }

    [Fact]
    public async Task InvokeAsync_NonClientCancellation_Rethrows()
    {
        // Arrange — cancellation NOT triggered by client abort (RequestAborted is not cancelled)
        var context = new DefaultHttpContext();

        var middleware = new OperationCancelledMiddleware(
            _ => throw new OperationCanceledException("timeout or other source"),
            this.loggerMock.Object);

        // Act & Assert — should rethrow because RequestAborted is not cancelled
        await Should.ThrowAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_OtherException_Rethrows()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var middleware = new OperationCancelledMiddleware(
            _ => throw new InvalidOperationException("Something went wrong"),
            this.loggerMock.Object);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
