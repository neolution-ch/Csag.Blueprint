namespace Csag.Blueprint.Tests.Shared.Session;

/// <summary>
/// A copy of the session state the package-internal service-account session manager resolves, readable from tests.
/// </summary>
/// <param name="ServiceAccountId">The account the session belongs to.</param>
/// <param name="ClientId">The account's client id.</param>
/// <param name="Name">The account's display name.</param>
/// <param name="TenantId">The account's tenant.</param>
/// <param name="Roles">The account's current roles.</param>
/// <param name="Permissions">The account's current explicit permissions.</param>
public sealed record ServiceAccountSessionSnapshot(
    Guid ServiceAccountId,
    string ClientId,
    string Name,
    Guid TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    /// <summary>
    /// Copies the internal result object's properties by name.
    /// </summary>
    /// <param name="validation">The manager's result.</param>
    /// <returns>The copy.</returns>
    internal static ServiceAccountSessionSnapshot From(object validation)
    {
        T Read<T>(string property) => (T)validation.GetType().GetProperty(property)!.GetValue(validation)!;

        return new ServiceAccountSessionSnapshot(
            Read<Guid>(nameof(ServiceAccountId)),
            Read<string>(nameof(ClientId)),
            Read<string>(nameof(Name)),
            Read<Guid>(nameof(TenantId)),
            Read<IReadOnlyList<string>>(nameof(Roles)),
            Read<IReadOnlyList<string>>(nameof(Permissions)));
    }
}
