namespace Csag.Blueprint.IntegrationTests.Persistence;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Time;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Time values (<see cref="PlaceTime"/>, <see cref="ZonedPlaceTime"/>, <see cref="ViewerTime"/>) on SQL Server:
/// stored in the columns the blueprint conventions name, read back, queried through their members in SQL,
/// updated in bulk, derived again by a maintenance job, and indexed through a migration.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TimeValuePersistenceTests(AppFixture app) : IntegrationTestBase(app)
{
    private const string NewYork = "America/New_York";

    private const string ZurichId = "Europe/Zurich";

    private static readonly TimeZoneInfo Zurich = FindZone(ZurichId);

    [Fact]
    public async Task Columns_AreNamedAfterThePropertyAndTypedPerMemberAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.CreateDbContextScope();

        var columns = await scope.Context.Database
            .SqlQuery<ColumnInfo>($"""
                SELECT COLUMN_NAME AS Name, DATA_TYPE AS DataType, CHARACTER_MAXIMUM_LENGTH AS MaxLength, IS_NULLABLE AS IsNullable
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME NOT IN ('Id', 'TenantId', 'Title')
                """)
            .ToListAsync(ct);

        columns.Select(c => $"{c.Name} {c.DataType}{(c.MaxLength is { } length ? $"({length})" : string.Empty)} {(c.IsNullable == "YES" ? "NULL" : "NOT NULL")}")
            .ShouldBe(
                [
                    "StartLocal datetime2 NOT NULL", "StartAt datetimeoffset NOT NULL",
                    "EndLocal datetime2 NULL", "EndAt datetimeoffset NULL",
                    "StandOpensLocal datetime2 NOT NULL", "StandOpensTimeZoneId nvarchar(100) NOT NULL", "StandOpensAt datetimeoffset NOT NULL",
                    "StandClosesLocal datetime2 NULL", "StandClosesTimeZoneId nvarchar(100) NULL", "StandClosesAt datetimeoffset NULL",
                    "BriefingLocal datetime2 NOT NULL", "BriefingTimeZoneId nvarchar(100) NOT NULL", "BriefingAt datetimeoffset NOT NULL",
                    "DebriefingLocal datetime2 NULL", "DebriefingTimeZoneId nvarchar(100) NULL", "DebriefingAt datetimeoffset NULL",
                ],
                ignoreOrder: true);
    }

    [Fact]
    public async Task RoundTrip_EveryKindRequiredAndOptional_ReadsBackEqualAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // 02:30 on 2026-10-25 happens twice in Zurich; the stored instant is the earlier occurrence.
        var appointment = NewAppointment("Round trip");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        appointment.StandCloses = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 18, 0, 0, DateTimeKind.Unspecified), NewYork);
        appointment.Debriefing = ViewerTime.Create(new DateTime(2026, 11, 20, 17, 30, 0, DateTimeKind.Unspecified), ZurichId);
        await this.AddAsync(appointment);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await scope.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);

        loaded.Start.ShouldBe(appointment.Start);
        loaded.End.ShouldBe(appointment.End);
        loaded.StandOpens.ShouldBe(appointment.StandOpens);
        loaded.StandCloses.ShouldBe(appointment.StandCloses);
        loaded.Briefing.ShouldBe(appointment.Briefing);
        loaded.Debriefing.ShouldBe(appointment.Debriefing);
        loaded.Start.Local.Kind.ShouldBe(DateTimeKind.Unspecified);
        loaded.Start.At.Offset.ShouldBe(TimeSpan.Zero);

        var stored = await scope.Context.Database
            .SqlQuery<StoredStart>($"SELECT StartLocal, StartAt, StandOpensTimeZoneId FROM Appointments WHERE Id = {appointment.Id}")
            .SingleAsync(ct);
        stored.StartLocal.ShouldBe(new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified));
        stored.StartAt.ShouldBe(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
        stored.StartAt.Offset.ShouldBe(TimeSpan.Zero);
        stored.StandOpensTimeZoneId.ShouldBe(NewYork);
    }

    [Fact]
    public async Task RoundTrip_OptionalTimeValuesLeftEmpty_ReadBackNullAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Without end");
        await this.AddAsync(appointment);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await scope.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);

        loaded.End.ShouldBeNull();
        loaded.StandCloses.ShouldBeNull();
        loaded.Debriefing.ShouldBeNull();
        loaded.Start.ShouldBe(appointment.Start);
    }

    [Fact]
    public async Task Update_ReplacingTimeValues_IsDetectedAndSavedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Rescheduled");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        await this.AddAsync(appointment);
        var newStart = PlaceTime.Create(new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Unspecified), Zurich);
        var debriefing = ViewerTime.Create(new DateTime(2026, 12, 2, 15, 0, 0, DateTimeKind.Unspecified), NewYork);

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            var tracked = await scope.Context.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);
            tracked.Start = newStart;
            tracked.End = null;
            tracked.Debriefing = debriefing;
            await scope.Context.SaveChangesAsync(ct);
        }

        using var verify = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await verify.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);
        loaded.Start.ShouldBe(newStart);
        loaded.End.ShouldBeNull();
        loaded.Debriefing.ShouldBe(debriefing);
    }

    [Fact]
    public async Task Query_FilteringAndSortingOnMembers_RunsInSqlAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Night starts on the 25th in Zurich but on the 24th in UTC; Late starts on the 24th in both.
        await this.AddAsync(
            NewAppointment("Morning", new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Unspecified)),
            NewAppointment("Night", new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Unspecified)),
            NewAppointment("Late", new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Unspecified)));
        var fromInstant = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var untilReading = new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Unspecified);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var query = scope.Context.Appointments
            .Where(a => a.Start.At >= fromInstant)
            .Where(a => a.Start.Local < untilReading)
            .OrderBy(a => a.Start.At)
            .Select(a => a.Title);
        var sql = query.ToQueryString();

        (await query.ToListAsync(ct)).ShouldBe(["Night"]);
        sql.ShouldContain("[a].[StartAt] >= @");
        sql.ShouldContain("[a].[StartLocal] < @");
        sql.ShouldContain("ORDER BY [a].[StartAt]");

        var byInstantDescending = scope.Context.Appointments.OrderByDescending(a => a.Start.At).Select(a => a.Title);
        (await byInstantDescending.ToListAsync(ct)).ShouldBe(["Morning", "Night", "Late"]);
    }

    [Fact]
    public async Task Query_ProjectionIntoFlatDto_ReadsOnlyTheMemberColumnsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Projected");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        var withoutEnd = NewAppointment("Projected without end");
        await this.AddAsync(appointment, withoutEnd);

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        var query = scope.Context.Appointments
            .Where(a => a.Title.StartsWith("Projected"))
            .OrderBy(a => a.Title)
            .Select(a => new AppointmentRow
            {
                Title = a.Title,
                StartLocal = a.Start.Local,
                StartAt = a.Start.At,
                EndAt = a.End != null ? a.End.At : null,
                StandOpensLocal = a.StandOpens.Local,
                StandOpensTimeZoneId = a.StandOpens.TimeZoneId,
            });
        var sql = query.ToQueryString();
        var rows = await query.ToListAsync(ct);

        rows.Count.ShouldBe(2);
        rows[0].StartLocal.ShouldBe(appointment.Start.Local);
        rows[0].StartAt.ShouldBe(appointment.Start.At);
        rows[0].EndAt.ShouldBe(appointment.End.At);
        rows[0].StandOpensLocal.ShouldBe(appointment.StandOpens.Local);
        rows[0].StandOpensTimeZoneId.ShouldBe(NewYork);
        rows[1].EndAt.ShouldBeNull();

        // Only the selected members are read: the other time values' columns are not in the statement.
        sql.ShouldContain("[a].[StartLocal]");
        sql.ShouldContain("[a].[StandOpensTimeZoneId]");
        sql.ShouldNotContain("[a].[BriefingLocal]");
        sql.ShouldNotContain("[a].[DebriefingAt]");
    }

    [Fact]
    public async Task Query_OptionalTimeValueIsNull_RunsInSqlAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var withEnd = NewAppointment("With end");
        withEnd.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        await this.AddAsync(withEnd, NewAppointment("Open ended"));

        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);

        (await scope.Context.Appointments.Where(a => a.End == null).Select(a => a.Title).ToListAsync(ct)).ShouldBe(["Open ended"]);
        (await scope.Context.Appointments.Where(a => a.End != null && a.End.At > a.Start.At).Select(a => a.Title).ToListAsync(ct)).ShouldBe(["With end"]);
    }

    [Fact]
    public async Task Index_CreatedByAMigrationOnTheAtColumn_CoversItAsync()
    {
        // EF Core 10 cannot declare an index over a complex type's member (HasIndex(a => a.Start.At) throws), so
        // the application creates it in its migration on the column the convention names.
        var ct = TestContext.Current.CancellationToken;
        using var scope = this.App.CreateDbContextScope();
        var migrationBuilder = new MigrationBuilder(scope.Context.Database.ProviderName);
        migrationBuilder.CreateIndex(name: "IX_Appointments_StartAt", table: "Appointments", column: "StartAt");

        foreach (var command in scope.Context.GetService<IMigrationsSqlGenerator>().Generate(migrationBuilder.Operations, scope.Context.Model))
        {
            await scope.Context.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        }

        var indexedColumns = await scope.Context.Database
            .SqlQuery<string>($"""
                SELECT c.name AS Value
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.name = 'IX_Appointments_StartAt'
                """)
            .ToListAsync(ct);
        indexedColumns.ShouldBe(["StartAt"]);
    }

    [Fact]
    public async Task ExecuteUpdate_SettingTimeValues_WritesAllTheirColumnsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Bulk rescheduled");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        await this.AddAsync(appointment);
        var newStart = PlaceTime.Create(new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Unspecified), Zurich);
        var newStand = ZonedPlaceTime.Create(new DateTime(2026, 12, 3, 8, 0, 0, DateTimeKind.Unspecified), "Asia/Tokyo");

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            var updated = await scope.Context.Appointments
                .Where(a => a.Id == appointment.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(a => a.Start, newStart)
                        .SetProperty(a => a.StandOpens, newStand),
                    ct);
            updated.ShouldBe(1);
        }

        using var verify = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await verify.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);
        loaded.Start.ShouldBe(newStart);
        loaded.StandOpens.ShouldBe(newStand);
        loaded.End.ShouldBe(appointment.End);
    }

    [Fact]
    public async Task ExecuteUpdate_ClearingAnOptionalTimeValue_NullsItsColumnsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Bulk open ended");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);
        await this.AddAsync(appointment);

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            await scope.Context.Appointments
                .Where(a => a.Id == appointment.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.End, (PlaceTime?)null), ct);
        }

        using var verify = this.App.CreateDbContextScope(SeedData.TenantAId);
        (await verify.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct)).End.ShouldBeNull();
    }

    [Fact]
    public async Task Rederive_RowStoredUnderOlderRules_IsSavedWithTheShiftedReadingAsync()
    {
        // The job an application runs after a time zone data update: rows are read back as stored, without
        // deriving anything, and each value is derived again. Here 02:30 on 2027-03-28 is stored as if Zurich
        // had no daylight saving time; under the host's rules it falls in the spring-forward gap.
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment("Stored under older rules");
        await this.AddAsync(appointment);
        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            await scope.Context.Database.ExecuteSqlAsync(
                $"UPDATE Appointments SET StartLocal = '2027-03-28T02:30:00', StartAt = '2027-03-28T01:30:00+00:00', BriefingLocal = '2027-03-28T02:30:00', BriefingAt = '2027-03-28T01:30:00+00:00', BriefingTimeZoneId = {ZurichId} WHERE Id = {appointment.Id}",
                ct);
        }

        using (var scope = this.App.CreateDbContextScope(SeedData.TenantAId))
        {
            var stored = await scope.Context.Appointments.SingleAsync(a => a.Id == appointment.Id, ct);
            stored.Start.Local.ShouldBe(new DateTime(2027, 3, 28, 2, 30, 0, DateTimeKind.Unspecified), "rows are materialized as stored");

            var (start, startShifted) = stored.Start.Rederive(Zurich);
            var (briefing, briefingShifted) = stored.Briefing.Rederive();
            stored.Start = start;
            stored.Briefing = briefing;
            await scope.Context.SaveChangesAsync(ct);

            startShifted.ShouldBeTrue();
            briefingShifted.ShouldBeTrue();
        }

        using var verify = this.App.CreateDbContextScope(SeedData.TenantAId);
        var loaded = await verify.Context.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id, ct);
        loaded.Start.ShouldBe(PlaceTime.Create(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified), Zurich));
        loaded.Briefing.ShouldBe(ViewerTime.Create(new DateTime(2027, 3, 28, 3, 30, 0, DateTimeKind.Unspecified), ZurichId));
    }

    [Fact]
    public async Task Audit_SavingTimeValues_RecordsTheirColumnValuesAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var appointment = NewAppointment($"Audited {Guid.NewGuid():N}");
        appointment.End = PlaceTime.Create(new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Unspecified), Zurich);

        // The host-registered context carries the audit save interceptor, unlike the fixture's scope.
        using var serviceScope = this.App.Services.CreateScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();
        TenantContext.SetTenant(SeedData.TenantAId);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(ct);

        var jsonData = await context.AuditLogs
            .Where(a => a.JsonData.Contains(appointment.Title))
            .Select(a => a.JsonData)
            .SingleAsync(ct);

        jsonData.ShouldContain("2026-10-25T02:30:00");
        jsonData.ShouldContain("2026-10-25T04:00:00");
        jsonData.ShouldContain(NewYork);
    }

    private static TestAppointment NewAppointment(string title, DateTime? start = null)
    {
        return new TestAppointment
        {
            Id = Guid.NewGuid(),
            TenantId = SeedData.TenantAId,
            Title = title,
            Start = PlaceTime.Create(start ?? new DateTime(2026, 10, 25, 2, 30, 0, DateTimeKind.Unspecified), Zurich),
            StandOpens = ZonedPlaceTime.Create(new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Unspecified), NewYork),
            Briefing = ViewerTime.Create(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Unspecified), ZurichId),
        };
    }

    private static TimeZoneInfo FindZone(string id)
    {
        return ZonedTime.TryFindIanaTimeZone(id, out var zone)
            ? zone
            : throw new InvalidOperationException($"The host cannot resolve the IANA zone '{id}'; install time zone data.");
    }

    private async Task AddAsync(params TestAppointment[] appointments)
    {
        using var scope = this.App.CreateDbContextScope(SeedData.TenantAId);
        scope.Context.Appointments.AddRange(appointments);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Materialized by EF Core from a raw SQL query.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S3459:Unassigned members should be removed", Justification = "Assigned by EF Core through reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S1144:Unused private types or members should be removed", Justification = "The setters are called by EF Core through reflection.")]
    private sealed class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public int? MaxLength { get; set; }

        public string IsNullable { get; set; } = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Materialized by EF Core from a raw SQL query.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S3459:Unassigned members should be removed", Justification = "Assigned by EF Core through reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S1144:Unused private types or members should be removed", Justification = "The setters are called by EF Core through reflection.")]
    private sealed class StoredStart
    {
        public DateTime StartLocal { get; set; }

        public DateTimeOffset StartAt { get; set; }

        public string StandOpensTimeZoneId { get; set; } = string.Empty;
    }

    private sealed class AppointmentRow
    {
        public string Title { get; set; } = string.Empty;

        public DateTime StartLocal { get; set; }

        public DateTimeOffset StartAt { get; set; }

        public DateTimeOffset? EndAt { get; set; }

        public DateTime StandOpensLocal { get; set; }

        public string StandOpensTimeZoneId { get; set; } = string.Empty;
    }
}
