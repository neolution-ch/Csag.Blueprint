namespace Csag.Blueprint.TestHost;
#pragma warning disable S2339 // Public constant members should not be used — string constants are deliberate so tests can use them in attributes and switch expressions.

using System.Globalization;

/// <summary>
/// Well-known identifiers and credentials for the deterministic data the host seeds at startup.
/// Integration tests reference these constants instead of querying for the seeded rows, so the
/// seeded world stays a stable, discoverable contract: two tenants, four users with one shared
/// password, eight vehicles (six in tenant A, two in tenant B), and a handful of translations.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// The password shared by every seeded user.
    /// </summary>
    public const string DefaultPassword = "Test@123";

    /// <summary>
    /// Email of the seeded user holding the TenantViewer role in tenant A.
    /// </summary>
    public const string ViewerAEmail = "viewerA@test.local";

    /// <summary>
    /// Email of the seeded user holding the TenantManager role in tenant A.
    /// </summary>
    public const string ManagerAEmail = "managerA@test.local";

    /// <summary>
    /// Email of the seeded user holding the TenantManager role in tenant B.
    /// </summary>
    public const string ManagerBEmail = "managerB@test.local";

    /// <summary>
    /// Email of the seeded user holding the platform-scope PlatformAdmin role. The user is a member
    /// of tenant A (so login resolves an active tenant) but holds no tenant-scoped role there, which
    /// keeps the platform and tenant capability surfaces cleanly separated for authorization tests.
    /// </summary>
    public const string PlatformAdminEmail = "platform@test.local";

    /// <summary>
    /// Number of vehicles seeded into tenant A.
    /// </summary>
    public const int TenantAVehicleCount = 6;

    /// <summary>
    /// Number of vehicles seeded into tenant B.
    /// </summary>
    public const int TenantBVehicleCount = 2;

    /// <summary>
    /// Identifier of tenant A, the tenant most seeded users belong to.
    /// </summary>
    public static readonly Guid TenantAId = Guid.Parse("11111111-1111-1111-1111-111111111111", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of tenant B, used for cross-tenant isolation assertions.
    /// </summary>
    public static readonly Guid TenantBId = Guid.Parse("22222222-2222-2222-2222-222222222222", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of the <see cref="ViewerAEmail"/> user.
    /// </summary>
    public static readonly Guid ViewerAUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of the <see cref="ManagerAEmail"/> user.
    /// </summary>
    public static readonly Guid ManagerAUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of the <see cref="ManagerBEmail"/> user.
    /// </summary>
    public static readonly Guid ManagerBUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of the <see cref="PlatformAdminEmail"/> user.
    /// </summary>
    public static readonly Guid PlatformAdminUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004", CultureInfo.InvariantCulture);

    /// <summary>
    /// Identifier of the "City Bike" vehicle seeded into tenant A, for get-by-id style assertions.
    /// </summary>
    public static readonly Guid CityBikeVehicleId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001", CultureInfo.InvariantCulture);
}
#pragma warning restore S2339 // Public constant members should not be used
