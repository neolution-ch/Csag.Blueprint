namespace Csag.Blueprint.IntegrationTests.Audit;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for Audit.NET audit logging behavior. Verifies that EF Core entity changes
/// produce rows in the BlueprintAuditLogs table with the expected user identity and correlation ID
/// enrichment, and pins which HTTP traffic reaches the audit log: HttpAuditMiddleware records a
/// request whose final status code is in HttpAuditOptions.AuditedStatusCodes, 401 and 403 by
/// default, and records nothing for a request outside that set.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class AuditTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);

    [Fact]
    public async Task SaveChanges_StoresEfAuditLogEntryAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — create a vehicle directly through the host-registered context, which carries
        // the audit save interceptor (unlike the fixture's plain database scope).
        using var scope = this.App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        TenantContext.SetTenant(SeedData.TenantAId);

        var vehicle = new TestVehicle
        {
            Id = Guid.NewGuid(),
            Name = "Audit Test Direct SaveChanges",
            Kind = TestVehicleKind.Kayak,
            Capacity = 2,
            PricePerHour = 15.00m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        context.Vehicles.Add(vehicle);

        // Act — SaveChanges triggers the audit save-changes interceptor.
        await context.SaveChangesAsync(ct);

        // Assert — an audit log entry with the entity data was created. Entity events use the
        // "{context}:{database}" event type template.
        var auditLogs = await context.AuditLogs
            .Where(a => a.JsonData.Contains("Audit Test Direct SaveChanges"))
            .ToListAsync(ct);

        auditLogs.ShouldNotBeEmpty("SaveChanges should generate an EF audit log entry via the interceptor");
        auditLogs.First().EventType.ShouldStartWith("TestDbContext");
    }

    [Fact]
    public async Task ForbiddenRequest_AuditLogIncludesUserIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — ViewerA is authenticated but lacks the vehicles:manage permission the create
        // endpoint requires, so authorization denies the request with 403 and the middleware
        // records it.
        var response = await this.App.ViewerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Denied Mutation Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: ct);

        // Assert — the HTTP audit entry for this exact request (keyed by correlation ID) captures
        // the acting user's ID from the session claims. Authentication runs before authorization,
        // so a denied request still carries the principal.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:POST:", ct);

        auditLog.UserId.ShouldBe(SeedData.ViewerAUserId.ToString(), "Audit log should capture the exact authenticated user ID");
    }

    [Fact]
    public async Task ForbiddenRequest_AuditLogCapturesUserEmailAndDisplayNameAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authenticated request that authorization denies with 403.
        var response = await this.App.ViewerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Denied Enrichment Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: ct);

        // Assert — the entry holds the email address and the display name. The middleware takes
        // both from the claims; TestUser does not override DisplayName, so the Blueprint base
        // user's fallback makes the display name equal to the email address.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:POST:", ct);

        ReadEnrichmentField(auditLog, "UserEmail")
            .ShouldBe(SeedData.ViewerAEmail, "Audit log should capture the authenticated user's email from ClaimTypes.Email");
        ReadEnrichmentField(auditLog, "UserDisplayName")
            .ShouldBe(SeedData.ViewerAEmail, "Audit log should capture the authenticated user's display name from ClaimTypes.Name");
    }

    [Fact]
    public async Task HttpEndpoint_EntityChangeAuditEntry_SharesActingUserAndCorrelationWithRequestAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authenticated mutation that writes an entity. The EF interceptor audits the
        // write itself, independently of the status codes the HTTP middleware records.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Audited Entity Change Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, cancellationToken: ct);

        // Assert — the EF entity event carries the same correlation ID and acting user as the
        // request, because the audit scope enrichment reads both from the ambient HTTP context.
        var entityAuditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "TestDbContext", ct);

        entityAuditLog.JsonData.ShouldContain("Audited Entity Change Vehicle");
        entityAuditLog.UserId.ShouldBe(SeedData.ManagerAUserId.ToString(), "The entity-change audit entry should carry the acting user of the request");
    }

    [Fact]
    public async Task ForbiddenRequest_AuditLogIncludesCorrelationIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an audited request; the correlation middleware generates an ID when none is sent.
        var response = await this.App.ViewerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Denied Correlation Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: ct);

        // Assert — the correlation ID from the response header identifies the stored audit entry.
        var correlationId = GetCorrelationId(response);
        var auditLog = await this.FindAuditLogAsync(correlationId, "HTTP:POST:", ct);

        auditLog.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task UnauthenticatedRequest_RejectedByAuthorization_IsAuditedWithoutUserIdentityAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an anonymous request to a protected endpoint is rejected with 401. The middleware
        // wraps the authorization middleware, so it still sees the denied response.
        var response = await this.App.AnonymousClient.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);

        // Assert — the denial is audited, but there is no user identity to capture.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:GET:", ct);

        auditLog.UserId.ShouldBeNull("An anonymous request has no user identity for the audit log to capture");
    }

    [Fact]
    public async Task SuccessfulRequest_LeavesNoHttpAuditEntryAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authorized request whose status code is outside AuditedStatusCodes. A read is
        // used so that no EF entity event competes with the HTTP event for the correlation ID.
        var response = await this.App.ManagerAClient.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);

        // Assert — the middleware records nothing for it.
        await this.ShouldHaveNoHttpAuditLogAsync(GetCorrelationId(response), ct);
    }

    [Fact]
    public async Task EndpointProducedUnauthorized_IsAuditedOnceAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — the login endpoint is anonymous, so neither CSRF nor authorization ends the request
        // early: the 401 for a wrong password comes from the endpoint itself, at the far end of the
        // pipeline. Every HttpAuditMiddleware in the pipeline therefore sees it, which makes this
        // request the one that reveals how often the middleware is registered.
        var response = await this.App.AnonymousClient.PostAsJsonAsync(
            LoginUri,
            new LoginRequest { Email = SeedData.ViewerAEmail, Password = "Wrong@123" },
            BlueprintJsonOptions.Default,
            ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: ct);

        // Assert — one entry for the request, not one per registration.
        await this.ShouldHaveOneHttpAuditLogAsync(GetCorrelationId(response), ct);
    }

    private static CreateVehicleRequest CreateVehicleRequestFor(string name) => new()
    {
        Name = name,
        Kind = TestVehicleKind.Bicycle,
        Capacity = 1,
        PricePerHour = 9.50m,
        IsActive = true,
        AcquiredAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// Reads the correlation ID from a response. The correlation ID identifies the audit entries
    /// of that exact request, so an entry from another request cannot give a wrong result.
    /// </summary>
    private static string GetCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("X-Correlation-ID", out var values)
            .ShouldBeTrue("Response should include the X-Correlation-ID header the audit entry is keyed by");

        var correlationId = values!.First();
        correlationId.ShouldNotBeNullOrEmpty("The X-Correlation-ID header should have a value");
        return correlationId;
    }

    /// <summary>
    /// Reads one enrichment field from a stored audit event. Audit.NET serializes custom fields as
    /// JSON extension data, so the fields sit at the root of <c>JsonData</c>, not in a column.
    /// </summary>
    private static string? ReadEnrichmentField(BlueprintAuditLog auditLog, string fieldName)
    {
        using var json = JsonDocument.Parse(auditLog.JsonData);

        json.RootElement.TryGetProperty(fieldName, out var value)
            .ShouldBeTrue($"Audit event JSON should carry '{fieldName}' at its root");

        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    /// <summary>
    /// Finds the audit log entry for one request by correlation ID and event type prefix. The HTTP
    /// audit scope is saved when the middleware unwinds, which can complete marginally after the
    /// client has already received the response, so this polls briefly instead of reading once.
    /// </summary>
    private async Task<BlueprintAuditLog> FindAuditLogAsync(string correlationId, string eventTypePrefix, CancellationToken ct)
    {
        const int maxAttempts = 20;

        BlueprintAuditLog? auditLog = null;
        for (var attempt = 0; attempt < maxAttempts && auditLog is null; attempt++)
        {
            using var scope = this.App.CreateDbContextScope();
            auditLog = await scope.Context.AuditLogs
                .Where(a => a.CorrelationId == correlationId && a.EventType.StartsWith(eventTypePrefix))
                .FirstOrDefaultAsync(ct);

            if (auditLog is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            }
        }

        auditLog.ShouldNotBeNull($"An audit entry with correlation ID '{correlationId}' and event type prefix '{eventTypePrefix}' should exist");
        return auditLog;
    }

    /// <summary>
    /// Asserts that no HTTP audit entry exists for one request. The audit scope is saved when the
    /// middleware unwinds, which can complete after the client has already received the response,
    /// so reading once would pass while the write was merely still in flight. This watches for a
    /// window instead, and fails as soon as an entry appears. Only "HTTP:" events are considered,
    /// because an EF entity event of the same request shares its correlation ID.
    /// </summary>
    private async Task ShouldHaveNoHttpAuditLogAsync(string correlationId, CancellationToken ct)
    {
        const int maxAttempts = 8;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var scope = this.App.CreateDbContextScope();
            var auditEntryExists = await scope.Context.AuditLogs
                .AnyAsync(a => a.CorrelationId == correlationId && a.EventType.StartsWith("HTTP:"), ct);

            auditEntryExists.ShouldBeFalse($"A request whose status code is outside AuditedStatusCodes should leave no HTTP audit entry, but correlation ID '{correlationId}' has one");

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            }
        }
    }

    /// <summary>
    /// Asserts that an audited request left exactly one "HTTP:" entry. HttpAuditMiddleware belongs
    /// to the pipeline once, so a second entry under the same correlation ID means it is registered
    /// more than once. The count is taken repeatedly rather than once, because a second registration
    /// writes its entry moments after the first, as the outer copy unwinds. Only "HTTP:" events are
    /// counted, because an EF entity event of the same request shares its correlation ID.
    /// </summary>
    private async Task ShouldHaveOneHttpAuditLogAsync(string correlationId, CancellationToken ct)
    {
        const int maxAttempts = 8;

        // The entry the request is expected to produce gets the same grace period as every other
        // lookup in this class before the count below has to see it.
        await this.FindAuditLogAsync(correlationId, "HTTP:", ct);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var scope = this.App.CreateDbContextScope();
            var httpAuditEntries = await scope.Context.AuditLogs
                .CountAsync(a => a.CorrelationId == correlationId && a.EventType.StartsWith("HTTP:"), ct);

            httpAuditEntries.ShouldBe(1, $"An audited request should leave exactly one HTTP audit entry, but correlation ID '{correlationId}' has {httpAuditEntries}; more than one means HttpAuditMiddleware is registered more than once");

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
            }
        }
    }
}
