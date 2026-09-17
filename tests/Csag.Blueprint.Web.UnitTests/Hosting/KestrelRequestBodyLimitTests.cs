namespace Csag.Blueprint.Web.UnitTests.Hosting;

using System.Net;
using Csag.Blueprint.Web.Extensions;
using Csag.Blueprint.Web.Options.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Behavioural coverage for the Kestrel transport-level request-body cap that an in-memory TestServer
/// cannot enforce. Boots a real Kestrel host wired by
/// <see cref="SecurityBuilderExtensions.ConfigureKestrelServerOptions"/> and posts over a real loopback
/// socket, proving an oversized (non-form) body is rejected with 413 Payload Too Large while a body within
/// the limit succeeds. This complements the options-level coverage of the configured limit value by
/// asserting the actual 413.
/// </summary>
public sealed class KestrelRequestBodyLimitTests
{
    private const int BytesPerMegabyte = 1024 * 1024;
    private const int MaxBodyMegabytes = 1;

    [Fact]
    public async Task OversizedBody_IsRejectedWith413_WhileBodyWithinLimitSucceedsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange — a real Kestrel host capped via the same production wiring the app uses.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var securitySettings = new SecuritySettings();
        securitySettings.RequestLimits.MaxRequestBodySizeMegabytes = MaxBodyMegabytes;
        builder.ConfigureKestrelServerOptions(securitySettings);

        await using var app = builder.Build();

        // A trivial endpoint that drains the body so Kestrel enforces the cap during the read.
        app.MapPost("/echo", async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);
            return Results.Ok();
        });

        await app.StartAsync(cancellationToken);
        var baseAddress = app.Urls.First();
        using var client = new HttpClient();

        // Act + Assert — an oversized non-form body trips the transport-level cap → 413 Payload Too Large.
        using var oversized = new ByteArrayContent(new byte[(MaxBodyMegabytes + 1) * BytesPerMegabyte]);
        var oversizedResponse = await client.PostAsync(new Uri($"{baseAddress}/echo"), oversized, cancellationToken);
        oversizedResponse.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        // Act + Assert — a body within the cap is accepted (positive control that the cap is not over-eager).
        using var withinLimit = new ByteArrayContent(new byte[BytesPerMegabyte / 4]);
        var withinLimitResponse = await client.PostAsync(new Uri($"{baseAddress}/echo"), withinLimit, cancellationToken);
        withinLimitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await app.StopAsync(cancellationToken);
    }
}
