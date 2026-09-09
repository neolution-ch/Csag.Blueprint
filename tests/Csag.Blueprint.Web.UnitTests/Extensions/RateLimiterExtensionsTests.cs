namespace Csag.Blueprint.Web.UnitTests.Extensions;

using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimiterExtensions"/>: the client partition-key resolution order
/// (edge-stamped header, then <c>RemoteIpAddress</c>, then no key at all) and the rejection response
/// shape (429 rather than the framework's 503 default, ProblemDetails body, <c>Retry-After</c>).
/// </summary>
public sealed class RateLimiterExtensionsTests
{
    private const string HeaderName = "X-Client-IP";

    [Fact]
    public void TryGetClientPartitionKey_TrustedHeaderPresent_PrefersHeaderOverRemoteIpAddress()
    {
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Request.Headers[HeaderName] = "203.0.113.2";

        var resolved = context.TryGetClientPartitionKey(HeaderName, out var partitionKey);

        resolved.ShouldBeTrue();
        partitionKey.ShouldBe("203.0.113.2");
    }

    [Fact]
    public void TryGetClientPartitionKey_HeaderRepeated_UsesLastEntry()
    {
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Request.Headers[HeaderName] = new[] { "203.0.113.7", "203.0.113.8" };

        context.TryGetClientPartitionKey(HeaderName, out var partitionKey).ShouldBeTrue();

        partitionKey.ShouldBe("203.0.113.8");
    }

    [Fact]
    public void TryGetClientPartitionKey_HeaderIsCommaSeparatedList_UsesLastToken()
    {
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Request.Headers[HeaderName] = "198.51.100.5, 203.0.113.9";

        context.TryGetClientPartitionKey(HeaderName, out var partitionKey).ShouldBeTrue();

        partitionKey.ShouldBe("203.0.113.9");
    }

    [Fact]
    public void TryGetClientPartitionKey_HeaderNotAnAddress_FallsBackToRemoteIpAddress()
    {
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Request.Headers[HeaderName] = "not-an-ip";

        context.TryGetClientPartitionKey(HeaderName, out var partitionKey).ShouldBeTrue();

        partitionKey.ShouldBe("203.0.113.1");
    }

    [Fact]
    public void TryGetClientPartitionKey_NoHeaderConfigured_UsesRemoteIpAddress()
    {
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Request.Headers[HeaderName] = "203.0.113.2";

        context.TryGetClientPartitionKey(trustedClientIpHeader: null, out var partitionKey).ShouldBeTrue();

        partitionKey.ShouldBe("203.0.113.1");
    }

    [Fact]
    public void TryGetClientPartitionKey_IPv4MappedToIPv6_NormalizesToIPv4()
    {
        var context = CreateContext(remoteIp: "::ffff:203.0.113.1");

        context.TryGetClientPartitionKey(trustedClientIpHeader: null, out var partitionKey).ShouldBeTrue();

        partitionKey.ShouldBe("203.0.113.1");
    }

    [Fact]
    public void TryGetClientPartitionKey_NoHeaderAndNoRemoteIpAddress_ReportsFailure()
    {
        var context = CreateContext(remoteIp: null);

        var resolved = context.TryGetClientPartitionKey(HeaderName, out var partitionKey);

        resolved.ShouldBeFalse();
        partitionKey.ShouldBeEmpty();
    }

    [Fact]
    public void UseBlueprintRejectionResponse_OverridesTheFrameworkDefaultOf503()
    {
        var options = new RateLimiterOptions();

        options.RejectionStatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);

        options.UseBlueprintRejectionResponse().ShouldBeSameAs(options);

        options.RejectionStatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task UseBlueprintRejectionResponse_OnRejected_WritesProblemDetailsWithRetryAfterAsync()
    {
        var options = new RateLimiterOptions().UseBlueprintRejectionResponse();
        var context = CreateContext(remoteIp: "203.0.113.1");
        var body = new MemoryStream();
        context.Response.Body = body;
        context.Items[CorrelationIdMiddleware.CorrelationIdKey] = "correlation-1";

        await options.OnRejected!(
            new OnRejectedContext { HttpContext = context, Lease = new TestLease(TimeSpan.FromSeconds(42)) },
            TestContext.Current.CancellationToken);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("42");
        (context.Response.ContentType ?? string.Empty).ShouldContain("application/problem+json");

        using var document = JsonDocument.Parse(body.ToArray());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status429TooManyRequests);
        root.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("correlationId").GetString().ShouldBe("correlation-1");
    }

    [Fact]
    public async Task UseBlueprintRejectionResponse_OnRejected_WithoutRetryAfterMetadata_OmitsTheHeaderAsync()
    {
        var options = new RateLimiterOptions().UseBlueprintRejectionResponse();
        var context = CreateContext(remoteIp: "203.0.113.1");
        context.Response.Body = new MemoryStream();

        await options.OnRejected!(
            new OnRejectedContext { HttpContext = context, Lease = new TestLease(retryAfter: null) },
            TestContext.Current.CancellationToken);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    private static DefaultHttpContext CreateContext(string? remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIp is null ? null : IPAddress.Parse(remoteIp);
        return context;
    }

    /// <summary>
    /// A rejected lease that optionally reports <see cref="MetadataName.RetryAfter"/>.
    /// </summary>
    private sealed class TestLease : RateLimitLease
    {
        private readonly TimeSpan? retryAfter;

        public TestLease(TimeSpan? retryAfter) => this.retryAfter = retryAfter;

        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames =>
            this.retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (this.retryAfter is not null && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = this.retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
