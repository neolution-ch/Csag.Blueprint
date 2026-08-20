namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.Delete;

using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Deletes a vehicle of the active tenant. Vehicles of other tenants are invisible through the
/// tenant query filter, so a cross-tenant id yields 404 rather than deleting foreign data.
/// </summary>
public sealed class DeleteVehicleEndpoint : EndpointWithoutRequest
{
    private readonly TestDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteVehicleEndpoint"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public DeleteVehicleEndpoint(TestDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Delete("/[namespace]/{id:guid}");
        this.Policies(PolicyNames.CanManageVehicles);
        this.Summary(s =>
        {
            s.Summary = "Delete a vehicle of the active tenant";
            s.Response(204, "Vehicle deleted");
            s.Response(404, "Vehicle not found in the active tenant");
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = this.Route<Guid>("id");

        var vehicle = await this.dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        this.dbContext.Vehicles.Remove(vehicle);
        await this.dbContext.SaveChangesAsync(ct);

        await this.Send.NoContentAsync(ct);
    }
}
