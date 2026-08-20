namespace Csag.Blueprint.TestHost.Endpoints.Vehicles.List;

using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Lists the vehicles of the caller's active tenant. The tenant scoping comes entirely from the
/// global tenant query filter over the ambient tenant context set by the tenant middleware.
/// </summary>
public sealed class ListVehiclesEndpoint : EndpointWithoutRequest<List<VehicleResponse>>
{
    private readonly TestDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListVehiclesEndpoint"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public ListVehiclesEndpoint(TestDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public override void Configure()
    {
        this.Get("/[namespace]");
        this.Policies(PolicyNames.CanReadVehicles);
        this.Summary(s =>
        {
            s.Summary = "List the vehicles of the active tenant";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var vehicles = await this.dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

        await this.Send.OkAsync(vehicles.Select(VehicleResponse.FromEntity).ToList(), ct);
    }
}
