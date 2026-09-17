namespace Csag.Blueprint.Web.UnitTests.Middleware;

using System.Globalization;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>
/// Unit tests for <see cref="CorrelationIdMiddleware"/>, covering the header-resolution branches
/// (X-Correlation-ID preferred over X-Request-ID, GUID generation when absent, truncation at the
/// maximum length), the places the resolved value is exposed (<c>HttpContext.Items</c>, the NLog
/// scope, the response header), and the guard that leaves a response header set downstream untouched.
/// The response-header echo runs in an <c>OnStarting</c> callback, which
/// <see cref="DefaultHttpContext"/> never fires on its own; a capturing response feature stands in
/// for the server and fires the callbacks the way a real host would.
/// </summary>
public sealed class CorrelationIdMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string RequestIdHeader = "X-Request-ID";
    private const int MaxCorrelationIdLength = 100;

    [Fact]
    public async Task InvokeAsync_XCorrelationIdHeaderPresent_UsesProvidedValue()
    {
        // Arrange
        var (context, _) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "test-correlation-123";

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdKey].ShouldBe("test-correlation-123");
    }

    [Fact]
    public async Task InvokeAsync_XRequestIdHeaderPresent_UsesProvidedValue()
    {
        // Arrange
        var (context, _) = CreateContext();
        context.Request.Headers[RequestIdHeader] = "test-request-456";

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdKey].ShouldBe("test-request-456");
    }

    [Fact]
    public async Task InvokeAsync_BothHeadersPresent_PrefersXCorrelationId()
    {
        // Arrange
        var (context, _) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "correlation-priority";
        context.Request.Headers[RequestIdHeader] = "request-fallback";

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdKey].ShouldBe("correlation-priority");
    }

    [Fact]
    public async Task InvokeAsync_NoCorrelationHeaders_GeneratesGuid()
    {
        // Arrange
        var (context, _) = CreateContext();

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;
        correlationId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(correlationId, CultureInfo.InvariantCulture, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhitespaceHeader_GeneratesGuid()
    {
        // Arrange — a present-but-blank header is treated the same as an absent one.
        var (context, _) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "   ";

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;
        Guid.TryParse(correlationId, CultureInfo.InvariantCulture, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LongCorrelationId_TruncatesToMaxLength()
    {
        // Arrange
        var longCorrelationId = new string('x', 150);
        var (context, responseFeature) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = longCorrelationId;

        // Act
        await CreateMiddleware().InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        // Assert — both the stored value and the response echo carry the truncated id.
        var stored = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;
        stored.ShouldNotBeNull();
        stored.Length.ShouldBe(MaxCorrelationIdLength);
        stored.ShouldBe(longCorrelationId[..MaxCorrelationIdLength]);
        context.Response.Headers[CorrelationIdHeader].ToString().ShouldBe(longCorrelationId[..MaxCorrelationIdLength]);
    }

    [Fact]
    public async Task InvokeAsync_EchoesCorrelationIdOnResponseHeader()
    {
        // Arrange
        var (context, responseFeature) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "response-header-test";

        // Act
        await CreateMiddleware().InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        // Assert
        context.Response.Headers[CorrelationIdHeader].ToString().ShouldBe("response-header-test");
    }

    [Fact]
    public async Task InvokeAsync_GeneratedCorrelationId_IsEchoedOnResponseHeader()
    {
        // Arrange
        var (context, responseFeature) = CreateContext();

        // Act
        await CreateMiddleware().InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        // Assert — the generated id and the echoed header are the same value.
        var stored = context.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;
        context.Response.Headers[CorrelationIdHeader].ToString().ShouldBe(stored);
    }

    [Fact]
    public async Task InvokeAsync_ResponseHeaderSetDownstream_IsNotOverwritten()
    {
        // Arrange — a downstream component already stamped its own correlation header.
        var (context, responseFeature) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "middleware-value";

        var middleware = new CorrelationIdMiddleware(innerContext =>
        {
            innerContext.Response.Headers[CorrelationIdHeader] = "downstream-value";
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStartingAsync();

        // Assert
        context.Response.Headers[CorrelationIdHeader].ToString().ShouldBe("downstream-value");
    }

    [Fact]
    public async Task InvokeAsync_ExposesCorrelationIdToDownstreamMiddleware()
    {
        // Arrange
        var (context, _) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "downstream-visible";
        string? seenByNext = null;

        var middleware = new CorrelationIdMiddleware(innerContext =>
        {
            seenByNext = innerContext.Items[CorrelationIdMiddleware.CorrelationIdKey] as string;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        seenByNext.ShouldBe("downstream-visible");
    }

    [Fact]
    public async Task InvokeAsync_PushesCorrelationIdIntoLogScopeForTheRequest()
    {
        // Arrange
        var (context, _) = CreateContext();
        context.Request.Headers[CorrelationIdHeader] = "log-scope-value";
        object? scopeValueDuringRequest = null;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            NLog.ScopeContext.TryGetProperty(CorrelationIdMiddleware.CorrelationIdKey, out scopeValueDuringRequest);
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — the property is visible to loggers during the request and popped afterwards.
        scopeValueDuringRequest.ShouldBe("log-scope-value");
        NLog.ScopeContext.TryGetProperty(CorrelationIdMiddleware.CorrelationIdKey, out _).ShouldBeFalse();
    }

    private static CorrelationIdMiddleware CreateMiddleware() => new(_ => Task.CompletedTask);

    private static (DefaultHttpContext Context, TriggerableResponseFeature ResponseFeature) CreateContext()
    {
        var context = new DefaultHttpContext();
        var responseFeature = new TriggerableResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        return (context, responseFeature);
    }

    /// <summary>
    /// Response feature that records <c>OnStarting</c> callbacks so a test can fire them the way a
    /// real server does just before the response is written.
    /// </summary>
    private sealed class TriggerableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> onStartingCallbacks = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            this.onStartingCallbacks.Add((callback, state));

        public async Task FireOnStartingAsync()
        {
            // Servers invoke OnStarting callbacks in reverse registration order.
            for (var i = this.onStartingCallbacks.Count - 1; i >= 0; i--)
            {
                var (callback, state) = this.onStartingCallbacks[i];
                await callback(state);
            }
        }
    }
}
