namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.Create;

using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;

/// <summary>
/// Creates a vehicle in the caller's active tenant. The entity's <c>TenantId</c> is stamped by the
/// tenant save interceptor from the ambient tenant context; the audit columns are stamped by the
/// auditable-timestamp interceptor with the current actor.
/// </summary>
public sealed class CreateVehicleEndpoint : Endpoint<CreateVehicleRequest, VehicleResponse>
{
    private readonly TestDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVehicleEndpoint"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public CreateVehicleEndpoint(TestDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Post("/[namespace]");
        this.Policies(PolicyNames.CanManageVehicles);
        this.Summary(s =>
        {
            s.Summary = "Create a vehicle in the active tenant";
            s.Response(201, "Vehicle created");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CreateVehicleRequest req, CancellationToken ct)
    {
        var vehicle = new TestVehicle
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Kind = req.Kind,
            Capacity = req.Capacity,
            PricePerHour = req.PricePerHour,
            IsActive = req.IsActive,
            AcquiredAt = req.AcquiredAt,
        };

        this.dbContext.Vehicles.Add(vehicle);
        await this.dbContext.SaveChangesAsync(ct);

        await this.Send.ResponseAsync(VehicleResponse.FromEntity(vehicle), 201, ct);
    }
}
