namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Entities;

/// <summary>
/// End-to-end tests for the RFC 9457 Problem Details error format: validation failures of the
/// create-vehicle validator return 400 with type/title/status and one error entry per failed rule
/// (duplicate field names allowed), error responses carry the correlation id, and 404/401/403
/// outcomes surface with the expected status codes.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ProblemDetailsErrorFormatTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task ValidationError_Returns400BadRequest_WithProblemDetailsFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, InvalidVehiclePayload(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var problemDetails = JsonDocument.Parse(content);

        // RFC 9457 requires these standard fields.
        problemDetails.RootElement.TryGetProperty("type", out _)
            .ShouldBeTrue("Problem Details response should include 'type' field");

        problemDetails.RootElement.TryGetProperty("title", out _)
            .ShouldBeTrue("Problem Details response should include 'title' field");

        problemDetails.RootElement.TryGetProperty("status", out var statusElement)
            .ShouldBeTrue("Problem Details response should include 'status' field");

        statusElement.GetInt32().ShouldBe(400);

        problemDetails.RootElement.TryGetProperty("errors", out var errorsElement)
            .ShouldBeTrue("Problem Details response should include 'errors' field for validation failures");

        // The validator messages are literal strings, so they arrive verbatim regardless of the
        // request culture.
        var errorMessages = ExtractErrorMessages(errorsElement);
        errorMessages.ShouldContain("Name is required.");
        errorMessages.ShouldContain("Kind must be a defined vehicle kind.");
        errorMessages.ShouldContain("Capacity must be greater than 0.");
        errorMessages.ShouldContain("PricePerHour must not be negative.");
    }

    [Fact]
    public async Task NotFoundError_Returns404_WithProblemDetailsFormat()
    {
        var ct = TestContext.Current.CancellationToken;
        const string nonExistentId = "99999999-9999-9999-9999-999999999999";

        using var response = await this.App.ViewerAClient.GetAsync(
            new Uri($"/api/vehicles/{nonExistentId}", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // FastEndpoints may return an empty body for 404; when a body is present it must be a
        // Problem Details payload with the matching status.
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrEmpty(content))
        {
            using var problemDetails = JsonDocument.Parse(content);
            if (problemDetails.RootElement.TryGetProperty("status", out var statusElement))
            {
                statusElement.GetInt32().ShouldBe(404);
            }
        }
    }

    [Fact]
    public async Task UnauthorizedError_Returns401_WithProblemDetailsFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        using var unauthenticatedClient = this.App.CreateClient();

        using var response = await unauthenticatedClient.GetAsync(VehiclesUri, ct);

        // 401 responses typically carry no body; the status code is the contract.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForbiddenError_Returns403_WithProblemDetailsFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        // The viewer may read but not manage vehicles; authorization is checked before resource
        // existence, so even a seeded vehicle id yields 403.
        using var response = await this.App.ViewerAClient.DeleteAsync(
            new Uri($"/api/vehicles/{SeedData.CityBikeVehicleId}", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrEmpty(content))
        {
            using var problemDetails = JsonDocument.Parse(content);
            if (problemDetails.RootElement.TryGetProperty("status", out var statusElement))
            {
                statusElement.GetInt32().ShouldBe(403);
            }
        }
    }

    [Fact]
    public async Task ValidationError_IncludesCorrelationId_InResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var correlationId = "validation-test-" + Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Post, VehiclesUri)
        {
            Content = JsonContent.Create(InvalidVehiclePayload()),
        };
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await this.App.ManagerAClient.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        response.Headers.TryGetValues("X-Correlation-ID", out var values)
            .ShouldBeTrue("Error response should include X-Correlation-ID header");
        values!.FirstOrDefault().ShouldBe(correlationId);
    }

    [Fact]
    public async Task ProblemDetails_HasJsonContentType()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, InvalidVehiclePayload(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // RFC 9457 specifies application/problem+json, but FastEndpoints may use application/json.
        response.Content.Headers.ContentType.ShouldNotBeNull();
        var mediaType = response.Content.Headers.ContentType.MediaType;
        (mediaType == "application/json" || mediaType == "application/problem+json")
            .ShouldBeTrue($"Content type should be JSON-based, got: {mediaType}");
    }

    [Fact]
    public async Task MultipleValidationErrors_AllIncluded_InProblemDetailsResponse()
    {
        var ct = TestContext.Current.CancellationToken;

        // The payload violates four independent rules: empty name, undefined kind, non-positive
        // capacity, and negative price.
        using var response = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, InvalidVehiclePayload(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var problemDetails = JsonDocument.Parse(content);

        problemDetails.RootElement.TryGetProperty("errors", out var errorsElement)
            .ShouldBeTrue("Problem Details should include 'errors' field");

        var errorCount = errorsElement.ValueKind == JsonValueKind.Object
            ? errorsElement.EnumerateObject().Count()
            : errorsElement.EnumerateArray().Count();

        errorCount.ShouldBeGreaterThanOrEqualTo(3, $"Expected at least 3 validation errors, got {errorCount}");
    }

    [Fact]
    public async Task MultipleValidationErrors_OnSameField_AllIncluded_InProblemDetailsResponse()
    {
        var ct = TestContext.Current.CancellationToken;

        // A whitespace-only name longer than the 100-character maximum triggers TWO separate rules
        // on Name: NotEmpty (whitespace counts as empty) and MaximumLength. With
        // AllowDuplicateErrors enabled the errors array must contain one entry per failed rule,
        // both named "Name".
        var request = new CreateVehicleRequest
        {
            Name = new string(' ', 150),
            Kind = TestVehicleKind.Bicycle,
            Capacity = 2,
            PricePerHour = 10.00m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        using var response = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, request, BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var problemDetails = JsonDocument.Parse(content);

        problemDetails.RootElement.TryGetProperty("errors", out var errorsElement)
            .ShouldBeTrue("Problem Details should include 'errors' field");

        errorsElement.ValueKind.ShouldBe(JsonValueKind.Array, "ProblemDetails errors should be an array");

        var nameErrors = errorsElement.EnumerateArray()
            .Where(e => e.TryGetProperty("name", out var name) &&
                        name.GetString()?.Equals("Name", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var errorDetail = $"Expected at least 2 validation errors for 'Name', got {nameErrors.Count}. Full errors: {errorsElement}";
        nameErrors.Count.ShouldBeGreaterThanOrEqualTo(2, errorDetail);
    }

    [Fact]
    public async Task ProblemDetails_Deserialization_WorksCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;

        using var response = await this.App.ManagerAClient.PostAsJsonAsync(VehiclesUri, InvalidVehiclePayload(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(ct);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Title.ShouldNotBeNull();
        problemDetails.Title.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Builds a create-vehicle payload violating four validator rules: empty name, undefined enum
    /// value, non-positive capacity, and negative price. Sent as an anonymous object so the
    /// out-of-range kind can be expressed as a raw number.
    /// </summary>
    private static object InvalidVehiclePayload() => new
    {
        name = string.Empty,
        kind = 999,
        capacity = 0,
        pricePerHour = -1m,
        isActive = true,
        acquiredAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// Flattens the Problem Details errors element into plain messages. FastEndpoints serializes
    /// errors as an array of { name, reason } entries; the dictionary form used by other
    /// ProblemDetails producers is handled as well so the assertion stays format-agnostic.
    /// </summary>
    private static List<string> ExtractErrorMessages(JsonElement errorsElement)
    {
        if (errorsElement.ValueKind == JsonValueKind.Object)
        {
            return errorsElement.EnumerateObject()
                .SelectMany(p => p.Value.EnumerateArray())
                .Select(e => e.GetString())
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();
        }

        if (errorsElement.ValueKind == JsonValueKind.Array)
        {
            return errorsElement.EnumerateArray()
                .Select(e => e.TryGetProperty("reason", out var reason) ? reason.GetString() : e.GetString())
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();
        }

        return [];
    }

    /// <summary>
    /// Represents the RFC 9457 Problem Details response structure.
    /// </summary>
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by JSON deserializer")]
    private sealed class ProblemDetailsResponse
    {
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        [SuppressMessage("CodeQuality", "S1144:Remove the unused private property", Justification = "Used by JSON deserializer")]
        public int Status { get; set; }

        [SuppressMessage("CodeQuality", "S1144:Remove the unused private property", Justification = "Used by JSON deserializer")]
        public string? Detail { get; set; }

        [SuppressMessage("CodeQuality", "S1144:Remove the unused private property", Justification = "Used by JSON deserializer")]
        public string? Instance { get; set; }

        [SuppressMessage("CodeQuality", "S1144:Remove the unused private property", Justification = "Used by JSON deserializer")]
        public JsonElement? Errors { get; set; }
    }
}
