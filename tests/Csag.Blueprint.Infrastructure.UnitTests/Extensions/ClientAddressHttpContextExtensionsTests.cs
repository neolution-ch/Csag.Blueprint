namespace Csag.Blueprint.Infrastructure.UnitTests.Extensions;

using System.Net;
using Csag.Blueprint.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for <see cref="ClientAddressHttpContextExtensions.TryGetClientAddress"/>, the rule rate limiting and
/// session tracking share to identify the calling client.
/// </summary>
public sealed class ClientAddressHttpContextExtensionsTests
{
    private const string Header = "X-Client-IP";

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("198.51.100.1, 203.0.113.7", "203.0.113.7")]
    [InlineData(" 203.0.113.7 ", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void TrustedHeader_YieldsItsLastAddress(string headerValue, string expected)
    {
        var context = CreateContext(remoteAddress: "10.0.0.1");
        context.Request.Headers[Header] = headerValue;

        context.TryGetClientAddress(Header, out var address).ShouldBeTrue();

        address.ShouldBe(IPAddress.Parse(expected));
    }

    [Fact]
    public void TrustedHeader_ReadsTheLastEntryWhenRepeated()
    {
        var context = CreateContext(remoteAddress: "10.0.0.1");
        context.Request.Headers[Header] = new[] { "198.51.100.1", "203.0.113.7" };

        context.TryGetClientAddress(Header, out var address).ShouldBeTrue();

        address.ShouldBe(IPAddress.Parse("203.0.113.7"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void MissingOrUnparseableHeader_FallsBackToTheConnectionAddress(string? headerValue)
    {
        var context = CreateContext(remoteAddress: "10.0.0.1");
        if (headerValue is not null)
        {
            context.Request.Headers[Header] = headerValue;
        }

        context.TryGetClientAddress(Header, out var address).ShouldBeTrue();

        address.ShouldBe(IPAddress.Parse("10.0.0.1"));
    }

    [Fact]
    public void NoTrustedHeaderConfigured_IgnoresAnyHeaderTheClientSent()
    {
        var context = CreateContext(remoteAddress: "10.0.0.1");
        context.Request.Headers[Header] = "203.0.113.7";

        context.TryGetClientAddress(trustedClientIpHeader: null, out var address).ShouldBeTrue();

        address.ShouldBe(IPAddress.Parse("10.0.0.1"));
    }

    [Fact]
    public void NoAddressAnywhere_ResolvesNothing()
    {
        var context = CreateContext(remoteAddress: null);

        context.TryGetClientAddress(Header, out var address).ShouldBeFalse();

        address.ShouldBeNull();
    }

    private static DefaultHttpContext CreateContext(string? remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteAddress is null ? null : IPAddress.Parse(remoteAddress);
        return context;
    }
}
