namespace Csag.Blueprint.IntegrationTests.Persistence;

using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Instants on SQL Server: every <see cref="DateTimeOffset"/> is written at offset zero, whatever offset it
/// carries, reads back as the same instant, and is still found by a query written with another offset.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class UtcInstantPersistenceTests(AppFixture app) : IntegrationTestBase(app)
{
    private const string ZurichId = "Europe/Zurich";

    private static readonly DateTimeOffset ConfirmedInZurich = new(2026, 7, 1, 10, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public async Task RoundTrip_InstantWithAnOffset_IsStoredAndReadBackAtOffsetZeroAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Confirmed at +02:00");
        appointment.ConfirmedAt = ConfirmedInZurich;
        await this.AddAsync(appointment);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await scope.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);

        loaded.ConfirmedAt.ShouldBe(ConfirmedInZurich, "the same instant");
        loaded.ConfirmedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        loaded.ConfirmedAt.Value.DateTime.ShouldBe(new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Unspecified));
        (await this.StoredOffsetMinutesAsync(appointment.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Query_ComparingWithAnInstantAtAnotherOffset_FindsTheRowAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment($"Confirmed {Guid.NewGuid():N}");
        appointment.ConfirmedAt = ConfirmedInZurich;
        await this.AddAsync(appointment);
        var from = ConfirmedInZurich.AddMinutes(-1);
        var until = ConfirmedInZurich.AddMinutes(1);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var mine = scope.Context.Appointments.Where(a => a.Title == appointment.Title);

        (await mine.CountAsync(a => a.ConfirmedAt == ConfirmedInZurich, ct)).ShouldBe(1);
        (await mine.CountAsync(a => a.ConfirmedAt >= from && a.ConfirmedAt < until, ct)).ShouldBe(1);
        (await mine.CountAsync(a => a.ConfirmedAt > ConfirmedInZurich, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteUpdate_SettingAnInstantWithAnOffset_StoresItAtOffsetZeroAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Confirmed in bulk");
        await this.AddAsync(appointment);

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            await scope.Context.Appointments
                .Where(a => a.Id == appointment.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ConfirmedAt, ConfirmedInZurich), ct);
        }

        (await this.StoredOffsetMinutesAsync(appointment.Id)).ShouldBe(0);
    }

    private static TestAppointment NewAppointment(string title)
    {
        var zurich = TimeZones.TryFindIanaTimeZone(ZurichId, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{ZurichId}'; install time zone data.");

        return new TestAppointment
        {
            Id = Guid.NewGuid(),
            TenantId = SeedData.TenantAId,
            Title = title,
            Start = PlaceTime.Create(new DateTime(2026, 7, 2, 9, 0, 0, DateTimeKind.Unspecified), zurich),
            StandOpens = ZonedTime.Create(new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Unspecified), "America/New_York"),
            Briefing = ZonedTime.Create(new DateTime(2026, 7, 1, 16, 0, 0, DateTimeKind.Unspecified), ZurichId),
        };
    }

    private async Task AddAsync(TestAppointment appointment)
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        scope.Context.Appointments.Add(appointment);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int?> StoredOffsetMinutesAsync(Guid id)
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        return await scope.Context.Database
            .SqlQuery<int?>($"SELECT DATEPART(TZOFFSET, ConfirmedAt) AS Value FROM Appointments WHERE Id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
