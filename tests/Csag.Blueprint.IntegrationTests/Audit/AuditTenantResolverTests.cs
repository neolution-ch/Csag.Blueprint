namespace Csag.Blueprint.IntegrationTests.Audit;

using System.Net;
using System.Net.Http.Json;
using Csag.Blueprint.Application.Json;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Auditing;
using Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for <c>BlueprintAuditOptions.EntityChangeTenantResolver</c>: which tenant an entity-change
/// audit event is recorded under. The host's resolver records the active tenant until a test pins another
/// through <see cref="PinnableAuditTenantResolver"/>.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class AuditTenantResolverTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task SaveChanges_WithoutAPinnedResolver_RecordsTheActiveTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act
        var auditLog = await this.SaveVehicleAsync(SeedData.TenantAId, ct);

        // Assert
        auditLog.TenantId.ShouldBe(SeedData.TenantAId);
    }

    [Fact]
    public async Task SaveChanges_RecordsTheTenantTheResolverReturnsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the answer depends on the active tenant passed in, so the recorded tenant proves both
        // that the resolver received it and that its answer replaced it. A save outside a request passes no
        // HttpContext, which the resolver must accept.
        var resolver = this.App.Services.GetRequiredService<PinnableAuditTenantResolver>();
        BlueprintAuditLog auditLog;

        // Act
        using (resolver.Pin((httpContext, activeTenantId) =>
            httpContext is null && activeTenantId == SeedData.TenantAId ? SeedData.TenantBId : activeTenantId))
        {
            auditLog = await this.SaveVehicleAsync(SeedData.TenantAId, ct);
        }

        // Assert
        auditLog.TenantId.ShouldBe(SeedData.TenantBId);
    }

    [Fact]
    public async Task HttpEndpoint_EntityChange_ResolverReceivesTheRequestAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — record changes saved by this endpoint outside every tenant. Without the resolver the
        // entry would carry the manager's active tenant, so a null TenantId shows the request reached it.
        var resolver = this.App.Services.GetRequiredService<PinnableAuditTenantResolver>();
        HttpResponseMessage response;

        // Act
        using (resolver.Pin((httpContext, activeTenantId) =>
            httpContext?.Request.Path.StartsWithSegments(VehiclesUri.OriginalString) == true ? null : activeTenantId))
        {
            response = await this.App.ManagerAClient.PostAsJsonAsync(
                VehiclesUri, CreateVehicleRequestFor("Audit Tenant Resolver Request"), BlueprintJsonOptions.Default, ct);
        }

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Created, cancellationToken: ct);

        // Assert
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "TestDbContext", ct);

        auditLog.TenantId.ShouldBeNull("The resolver recorded the change outside every tenant");
    }

    [Fact]
    public async Task ForbiddenRequest_HttpAuditEvent_KeepsTheRequestTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a resolver that would move every event it is asked about to tenant B.
        var resolver = this.App.Services.GetRequiredService<PinnableAuditTenantResolver>();
        HttpResponseMessage response;

        // Act — ViewerA lacks the permission the create endpoint requires, so the request is denied with
        // 403, writes no entity, and HttpAuditMiddleware records it.
        using (resolver.Pin((_, _) => SeedData.TenantBId))
        {
            response = await this.App.ViewerAClient.PostAsJsonAsync(
                VehiclesUri, CreateVehicleRequestFor("Audit Tenant Resolver Denied"), BlueprintJsonOptions.Default, ct);
        }

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: ct);

        // Assert — the resolver decides entity-change events only.
        var auditLog = await this.FindAuditLogAsync(GetCorrelationId(response), "HTTP:POST:", ct);

        auditLog.TenantId.ShouldBe(SeedData.TenantAId);
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

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("X-Correlation-ID", out var values)
            .ShouldBeTrue("Response should include the X-Correlation-ID header the audit entry is keyed by");

        return values!.First();
    }

    /// <summary>
    /// Saves a vehicle through the host-registered context, which carries the audit save interceptor, with
    /// <paramref name="activeTenantId"/> as the ambient tenant, and returns the audit entry it wrote.
    /// </summary>
    private async Task<BlueprintAuditLog> SaveVehicleAsync(Guid activeTenantId, CancellationToken ct)
    {
        using var scope = this.App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        TenantContext.SetTenant(activeTenantId);

        var marker = $"Audit tenant probe {Guid.NewGuid():N}";
        context.Vehicles.Add(new TestVehicle
        {
            Id = Guid.NewGuid(),
            Name = marker,
            Kind = TestVehicleKind.Kayak,
            Capacity = 2,
            PricePerHour = 15.00m,
            IsActive = true,
            AcquiredAt = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        await context.SaveChangesAsync(ct);

        return await context.AuditLogs.AsNoTracking().SingleAsync(a => a.JsonData.Contains(marker), ct);
    }

    /// <summary>
    /// Finds the audit entry for one request by correlation ID and event type prefix. The HTTP audit scope
    /// is saved when the middleware unwinds, which can complete after the client has the response, so this
    /// polls briefly instead of reading once.
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
