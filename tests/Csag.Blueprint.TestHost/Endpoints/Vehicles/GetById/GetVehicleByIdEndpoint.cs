namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.GetById;

using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Returns a single vehicle of the active tenant by id. A vehicle belonging to another tenant is
/// indistinguishable from a missing one — the tenant query filter hides it, so the response is 404.
/// </summary>
public sealed class GetVehicleByIdEndpoint : EndpointWithoutRequest<VehicleResponse>
{
    private readonly TestDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetVehicleByIdEndpoint"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public GetVehicleByIdEndpoint(TestDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]/{id:guid}");
        this.Policies(PolicyNames.CanReadVehicles);
        this.Summary(s =>
        {
            s.Summary = "Get a vehicle of the active tenant by id";
            s.Response(404, "Vehicle not found in the active tenant");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = this.Route<Guid>("id");

        var vehicle = await this.dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (vehicle is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(VehicleResponse.FromEntity(vehicle), ct);
    }
}
