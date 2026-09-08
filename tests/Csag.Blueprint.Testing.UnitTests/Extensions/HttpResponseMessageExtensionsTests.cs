namespace Csag.Blueprint.Testing.UnitTests.Extensions;

using System.Net;
using Csag.Blueprint.Testing.Extensions;

/// <summary>
/// Unit tests for <see cref="HttpResponseMessageExtensions.ShouldHaveStatusCodeAsync"/> verifying
/// that a matching status code passes silently and that a mismatch produces an assertion failure
/// carrying the expected status, the actual status, and the response body.
/// </summary>
public sealed class HttpResponseMessageExtensionsTests
{
    [Fact]
    public async Task ShouldHaveStatusCodeAsync_WithMatchingStatusCode_DoesNotThrow()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("irrelevant on success"),
        };

        // Act & Assert — completes without throwing.
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldHaveStatusCodeAsync_WithMismatch_FailureMessageContainsBothStatusCodesAndBody()
    {
        // Arrange
        const string body = """{"error":"vehicle not found"}""";
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(body),
        };

        // Act
        var exception = await Record.ExceptionAsync(() => response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK));

        // Assert — the failure message names both status codes and includes the body, so the
        // test log alone is enough to diagnose the failure without re-reading the response.
        var assertException = exception.ShouldBeOfType<ShouldAssertException>();
        assertException.Message.ShouldContain(nameof(HttpStatusCode.OK));
        assertException.Message.ShouldContain(nameof(HttpStatusCode.NotFound));
        assertException.Message.ShouldContain(body);
    }

    [Fact]
    public async Task ShouldHaveStatusCodeAsync_WithCustomMessage_PrependsItToTheBody()
    {
        // Arrange
        const string customMessage = "creating the vehicle should succeed";
        const string body = "validation failed";
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body),
        };

        // Act
        var exception = await Record.ExceptionAsync(() => response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, customMessage));

        // Assert — the custom message comes first, separated from the body dump.
        exception.ShouldBeOfType<ShouldAssertException>().Message.ShouldContain($"{customMessage} | Response body: {body}");
    }

    [Fact]
    public async Task ShouldHaveStatusCodeAsync_WithEmptyBody_StillFailsWithBothStatusCodes()
    {
        // Arrange — no content set: reading the body yields an empty string.
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        var exception = await Record.ExceptionAsync(() => response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK));

        // Assert
        var assertException = exception.ShouldBeOfType<ShouldAssertException>();
        assertException.Message.ShouldContain(nameof(HttpStatusCode.OK));
        assertException.Message.ShouldContain(nameof(HttpStatusCode.InternalServerError));
        assertException.Message.ShouldContain("Response body:");
    }

    [Fact]
    public async Task ShouldHaveStatusCodeAsync_WithWhitespaceCustomMessage_OmitsThePrefix()
    {
        // Arrange — a whitespace-only custom message is treated like no custom message at all.
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("nope"),
        };

        // Act
        var exception = await Record.ExceptionAsync(() => response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "   "));

        // Assert — no stray "   | " separator appears before the body dump.
        var assertException = exception.ShouldBeOfType<ShouldAssertException>();
        assertException.Message.ShouldContain("Response body: nope");
        assertException.Message.ShouldNotContain("| Response body:");
    }
}
