namespace Csag.Blueprint.Testing.Extensions;

using System.Net;
using Shouldly;

/// <summary>
/// Shouldly-style assertion extensions for <see cref="HttpResponseMessage"/>.
/// Automatically includes the response body in failure messages so you never have to read it manually.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Asserts that the response has the expected status code.
    /// On failure, the actual response body is included in the assertion message automatically.
    /// </summary>
    /// <param name="response">The HTTP response to assert against.</param>
    /// <param name="expected">The expected HTTP status code.</param>
    /// <param name="customMessage">An optional additional message to include in the failure output.</param>
    /// <param name="cancellationToken">The cancellation token observed while reading the response body.</param>
    public static async Task ShouldHaveStatusCodeAsync(this HttpResponseMessage response, HttpStatusCode expected, string? customMessage = null, CancellationToken cancellationToken = default)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        // Buffered content types complete ReadAsStringAsync synchronously without consulting the
        // token, so check it explicitly to guarantee cancellation surfaces on the failure path.
        cancellationToken.ThrowIfCancellationRequested();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(customMessage)
            ? $"Response body: {body}"
            : $"{customMessage} | Response body: {body}";

        response.StatusCode.ShouldBe(expected, message);
    }
}
