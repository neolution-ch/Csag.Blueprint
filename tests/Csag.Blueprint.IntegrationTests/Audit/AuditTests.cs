namespace Csag.Blueprint.IntegrationTests.Audit;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for Audit.NET audit logging behavior. Verifies that EF Core entity changes
/// and HTTP requests produce rows in the BlueprintAuditLogs table with the expected user identity
/// and correlation ID enrichment, and pins how unauthenticated traffic is (and is not) audited.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class AuditTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

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
    public async Task HttpEndpoint_AuditLogIncludesUserIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authenticated mutation; the HTTP audit middleware records the request.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Audited Mutation Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created);

        // Assert — the HTTP audit entry for this exact request (keyed by correlation ID) captures
        // the acting user's ID from the session claims.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:POST:", ct);

        auditLog.UserId.ShouldBe(SeedData.ManagerAUserId.ToString(), "Audit log should capture the exact authenticated user ID");
    }

    [Fact]
    public async Task HttpEndpoint_AuditLogCapturesUserEmailAndDisplayNameAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authenticated mutation; the HTTP audit middleware records the request.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Audited Enrichment Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created);

        // Assert — the entry holds the email address and the display name. The middleware takes
        // both from the claims; TestUser does not override DisplayName, so the Blueprint base
        // user's fallback makes the display name equal to the email address.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:POST:", ct);

        ReadEnrichmentField(auditLog, "UserEmail")
            .ShouldBe(SeedData.ManagerAEmail, "Audit log should capture the authenticated user's email from ClaimTypes.Email");
        ReadEnrichmentField(auditLog, "UserDisplayName")
            .ShouldBe(SeedData.ManagerAEmail, "Audit log should capture the authenticated user's display name from ClaimTypes.Name");
    }

    [Fact]
    public async Task HttpEndpoint_EntityChangeAuditEntry_SharesActingUserAndCorrelationWithRequestAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an authenticated mutation that writes an entity, so the same request produces both
        // an HTTP audit entry and an EF entity-change audit entry.
        var response = await this.App.ManagerAClient.PostAsJsonAsync(
            VehiclesUri, CreateVehicleRequestFor("Audited Entity Change Vehicle"), BlueprintJsonOptions.Default, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created);

        // Assert — the EF entity event carries the same correlation ID and acting user as the
        // request, because the audit scope enrichment reads both from the ambient HTTP context.
        var entityAuditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "TestDbContext", ct);

        entityAuditLog.JsonData.ShouldContain("Audited Entity Change Vehicle");
        entityAuditLog.UserId.ShouldBe(SeedData.ManagerAUserId.ToString(), "The entity-change audit entry should carry the acting user of the request");
    }

    [Fact]
    public async Task HttpEndpoint_AuditLogIncludesCorrelationIdAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — any audited request; the correlation middleware generates an ID when none is sent.
        var response = await this.App.ManagerAClient.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);

        // Assert — the correlation ID from the response header identifies the stored audit entry.
        var correlationId = GetCorrelationId(response);
        var auditLog = await this.FindAuditLogAsync(correlationId, "HTTP:GET:", ct);

        auditLog.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task AnonymousRequest_ToAnonymousEndpoint_IsAuditedWithoutUserIdentityAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an anonymous request to an endpoint that allows anonymous access, so it passes
        // authorization and reaches the audit middleware.
        var response = await this.App.AnonymousClient.GetAsync(new Uri("/api/localization/greeting", UriKind.Relative), ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);

        // Assert — the request is audited, but with no user identity to capture.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:GET:", ct);

        auditLog.UserId.ShouldBeNull("An anonymous request has no user identity for the audit log to capture");
    }

    [Fact]
    public async Task UnauthenticatedRequest_RejectedByAuthorization_LeavesNoAuditEntryAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act — an anonymous request to a protected endpoint is rejected with 401 by the
        // authorization middleware, which sits before the audit middleware in the pipeline.
        var response = await this.App.AnonymousClient.GetAsync(VehiclesUri, ct);
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized);

        // The correlation middleware runs first, so even the rejected request carries the header.
        var correlationId = GetCorrelationId(response);

        // Assert — the short-circuited request never reached the audit middleware, so no entry exists.
        using var scope = this.App.CreateDbContextScope();
        var auditEntryExists = await scope.Context.AuditLogs.AnyAsync(a => a.CorrelationId == correlationId, ct);

        auditEntryExists.ShouldBeFalse("A request rejected before the audit middleware should leave no audit entry");
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
}
