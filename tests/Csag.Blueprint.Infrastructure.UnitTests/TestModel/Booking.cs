namespace Csag.Blueprint.Infrastructure.UnitTests.TestModel;

using Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Tenant-owned, soft-deletable entity, so both named query filters apply to it.
/// </summary>
public sealed class Booking : IMustHaveTenant, ISoftDeletable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string Reference { get; set; } = string.Empty;
}
