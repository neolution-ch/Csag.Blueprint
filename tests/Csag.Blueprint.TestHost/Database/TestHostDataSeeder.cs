namespace Csag.Blueprint.TestHost.Database;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost.Localization;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds the deterministic test world described by <see cref="SeedData"/>: the shared role catalog,
/// two tenants, four users with tenant memberships and role assignments, vehicles in both tenants,
/// and translation rows exercising every localization fallback tier. Every step checks for existing
/// rows first, so re-running the seeder against an already-seeded database is a no-op.
/// </summary>
public sealed class TestHostDataSeeder
{
    private readonly TestDbContext dbContext;
    private readonly RoleManager<TestRole> roleManager;
    private readonly UserManager<TestUser> userManager;
    private readonly ITenantRoleService tenantRoleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHostDataSeeder"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    /// <param name="roleManager">The Identity role manager used to create the role catalog.</param>
    /// <param name="userManager">The Identity user manager used to create users with hashed passwords.</param>
    /// <param name="tenantRoleService">The Blueprint service assigning tenant-scoped roles.</param>
    public TestHostDataSeeder(
        TestDbContext dbContext,
        RoleManager<TestRole> roleManager,
        UserManager<TestUser> userManager,
        ITenantRoleService tenantRoleService)
    {
        this.dbContext = dbContext;
        this.roleManager = roleManager;
        this.userManager = userManager;
        this.tenantRoleService = tenantRoleService;
    }

    /// <summary>
    /// Seeds all test data idempotently.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await this.SeedRolesAsync();
        await this.SeedTenantsAsync(cancellationToken);
        await this.SeedUsersAsync(cancellationToken);
        await this.SeedVehiclesAsync(cancellationToken);
        await this.SeedTranslationsAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a vehicle row. <c>TenantId</c> is left unset on purpose — the tenant save interceptor
    /// stamps it from the ambient tenant context during save.
    /// </summary>
    private static TestVehicle NewVehicle(Guid id, string name, TestVehicleKind kind, int capacity, decimal pricePerHour, bool isActive, DateTime acquiredAt)
    {
        return new TestVehicle
        {
            Id = id,
            Name = name,
            Kind = kind,
            Capacity = capacity,
            PricePerHour = pricePerHour,
            IsActive = isActive,
            AcquiredAt = acquiredAt,
        };
    }

    /// <summary>
    /// Throws when an Identity operation failed, surfacing the errors instead of silently
    /// continuing with a half-seeded database.
    /// </summary>
    private static void EnsureSucceeded(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Seeding {what} failed: {errors}");
        }
    }

    /// <summary>
    /// Creates the role catalog rows (tenant-scoped and platform-scope roles alike live in the
    /// shared Identity role table).
    /// </summary>
    private async Task SeedRolesAsync()
    {
        foreach (var roleName in TestRoles.All)
        {
            if (!await this.roleManager.RoleExistsAsync(roleName))
            {
                var result = await this.roleManager.CreateAsync(new TestRole { Name = roleName });
                EnsureSucceeded(result, $"role '{roleName}'");
            }
        }
    }

    /// <summary>
    /// Creates tenants A and B with their well-known identifiers.
    /// </summary>
    private async Task SeedTenantsAsync(CancellationToken cancellationToken)
    {
        await this.EnsureTenantAsync(SeedData.TenantAId, "Tenant A", cancellationToken);
        await this.EnsureTenantAsync(SeedData.TenantBId, "Tenant B", cancellationToken);
    }

    /// <summary>
    /// Creates the four seeded users with their memberships and role assignments.
    /// </summary>
    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        await this.EnsureUserAsync(SeedData.ViewerAUserId, SeedData.ViewerAEmail, SeedData.TenantAId, TestRoles.TenantViewer, globalRole: null, cancellationToken);
        await this.EnsureUserAsync(SeedData.ManagerAUserId, SeedData.ManagerAEmail, SeedData.TenantAId, TestRoles.TenantManager, globalRole: null, cancellationToken);
        await this.EnsureUserAsync(SeedData.ManagerBUserId, SeedData.ManagerBEmail, SeedData.TenantBId, TestRoles.TenantManager, globalRole: null, cancellationToken);

        // The platform admin is a member of tenant A so login resolves an active tenant, but holds
        // no tenant-scoped role there — its only capability is the platform-scope one.
        await this.EnsureUserAsync(SeedData.PlatformAdminUserId, SeedData.PlatformAdminEmail, SeedData.TenantAId, tenantRole: null, globalRole: TestRoles.PlatformAdmin, cancellationToken);
    }

    /// <summary>
    /// Seeds the vehicles for both tenants. Vehicles are tenant-owned, so both the existence check
    /// (the global tenant query filter requires an ambient tenant) and the insert (the tenant save
    /// interceptor stamps <c>TenantId</c> from the ambient context) run with the owning tenant set.
    /// </summary>
    private async Task SeedVehiclesAsync(CancellationToken cancellationToken)
    {
        TestVehicle[] tenantAVehicles =
        [
            NewVehicle(SeedData.CityBikeVehicleId, "City Bike", TestVehicleKind.Bicycle, capacity: 1, pricePerHour: 6.50m, isActive: true, acquiredAt: new DateTime(2023, 3, 15, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Tandem Bike", TestVehicleKind.Bicycle, capacity: 2, pricePerHour: 9.00m, isActive: true, acquiredAt: new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Electric Scooter", TestVehicleKind.Scooter, capacity: 1, pricePerHour: 12.00m, isActive: true, acquiredAt: new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Cargo Scooter", TestVehicleKind.Scooter, capacity: 2, pricePerHour: 14.50m, isActive: false, acquiredAt: new DateTime(2021, 11, 5, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Lake Kayak", TestVehicleKind.Kayak, capacity: 2, pricePerHour: 18.00m, isActive: true, acquiredAt: new DateTime(2023, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Family Kayak", TestVehicleKind.Kayak, capacity: 4, pricePerHour: 25.00m, isActive: false, acquiredAt: new DateTime(2020, 4, 12, 0, 0, 0, DateTimeKind.Utc)),
        ];

        TestVehicle[] tenantBVehicles =
        [
            NewVehicle(Guid.NewGuid(), "Harbor Kayak", TestVehicleKind.Kayak, capacity: 2, pricePerHour: 16.00m, isActive: true, acquiredAt: new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            NewVehicle(Guid.NewGuid(), "Downtown Scooter", TestVehicleKind.Scooter, capacity: 1, pricePerHour: 11.00m, isActive: true, acquiredAt: new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc)),
        ];

        await this.EnsureVehiclesAsync(SeedData.TenantAId, tenantAVehicles, cancellationToken);
        await this.EnsureVehiclesAsync(SeedData.TenantBId, tenantBVehicles, cancellationToken);
    }

    /// <summary>
    /// Seeds the translation rows backing the localization fallback tests: one key with rows in
    /// both languages (the "de" row overrides), one key with a row only in the default language
    /// (other languages fall back to it), and one key with no rows at all (falls back to the
    /// code-defined default in <see cref="TranslationDefaults"/>).
    /// </summary>
    private async Task SeedTranslationsAsync(CancellationToken cancellationToken)
    {
        (string Key, string LanguageCode, string Value)[] rows =
        [
            (TranslationKeys.GreetingHello, "en", "Hello from the database"),
            (TranslationKeys.GreetingHello, "de", "Hallo aus der Datenbank"),
            (TranslationKeys.GreetingEnglishOnly, "en", "This value exists only in English"),
        ];

        foreach (var (key, languageCode, value) in rows)
        {
            var exists = await this.dbContext.Translations
                .AnyAsync(t => t.Key == key && t.LanguageCode == languageCode, cancellationToken);
            if (!exists)
            {
                this.dbContext.Translations.Add(new BlueprintTranslation
                {
                    Key = key,
                    LanguageCode = languageCode,
                    Value = value,
                });
            }
        }

        await this.dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a tenant if it does not exist yet.
    /// </summary>
    private async Task EnsureTenantAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        var exists = await this.dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!exists)
        {
            this.dbContext.Tenants.Add(new TestTenant { Id = tenantId, Name = name });
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Creates a user with the shared password, adds the tenant membership, and assigns the
    /// requested tenant-scoped and/or global role.
    /// </summary>
    private async Task EnsureUserAsync(Guid userId, string email, Guid tenantId, string? tenantRole, string? globalRole, CancellationToken cancellationToken)
    {
        var user = await this.userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new TestUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            EnsureSucceeded(await this.userManager.CreateAsync(user, SeedData.DefaultPassword), $"user '{email}'");
        }

        var isMember = await this.dbContext.TenantMemberships
            .AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId, cancellationToken);
        if (!isMember)
        {
            this.dbContext.TenantMemberships.Add(new BlueprintTenantMembership<TestUser, TestTenant>
            {
                UserId = user.Id,
                TenantId = tenantId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }

        if (tenantRole is not null)
        {
            // SetRolesAsync synchronises to the desired set, so it is naturally idempotent.
            await this.tenantRoleService.SetRolesAsync(user.Id, tenantId, [tenantRole], cancellationToken);
        }

        if (globalRole is not null && !await this.userManager.IsInRoleAsync(user, globalRole))
        {
            EnsureSucceeded(await this.userManager.AddToRoleAsync(user, globalRole), $"global role '{globalRole}' for '{email}'");
        }
    }

    /// <summary>
    /// Inserts the given vehicles for a tenant when that tenant has none yet, with the ambient
    /// tenant context set for the duration (required by both the query filter and the interceptor).
    /// </summary>
    private async Task EnsureVehiclesAsync(Guid tenantId, IReadOnlyList<TestVehicle> vehicles, CancellationToken cancellationToken)
    {
        TenantContext.SetTenant(tenantId);
        try
        {
            if (await this.dbContext.Vehicles.AnyAsync(cancellationToken))
            {
                return;
            }

            foreach (var vehicle in vehicles)
            {
                this.dbContext.Vehicles.Add(vehicle);
            }

            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            TenantContext.Clear();
        }
    }
}
