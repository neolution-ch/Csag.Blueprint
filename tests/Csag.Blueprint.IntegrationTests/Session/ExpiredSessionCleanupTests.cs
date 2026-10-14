namespace Csag.Blueprint.IntegrationTests.Session;

using System.Globalization;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Integration tests for <see cref="ExpiredSessionCleanupService{TContext}"/> against the real SQL container: what a
/// pass deletes, that it deletes in batches until done, and that the hosted loop runs on its interval and not at all
/// when switched off. Each test builds the job with its own fake clock; every expiry it writes lies far in the past,
/// so the set-based deletes can never reach the fixture's own sessions.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ExpiredSessionCleanupTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly DateTimeOffset ClockEpoch = new(2020, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Host_RunsTheCleanupJob()
    {
        this.App.Services.GetServices<IHostedService>().OfType<ExpiredSessionCleanupService<TestDbContext>>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RemoveExpiredSessions_DeletesUpToAndIncludingTheCurrentInstantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — three sessions straddling the clock instant by a second either side.
        var userId = Guid.NewGuid();
        var expiredKey = NewKey();
        var boundaryKey = NewKey();
        var liveKey = NewKey();
        await this.SeedSessionsAsync(userId, (expiredKey, ClockEpoch.AddSeconds(-1)), (boundaryKey, ClockEpoch), (liveKey, ClockEpoch.AddSeconds(1)));
        var job = this.CreateJob(new FakeTimeProvider(ClockEpoch));

        // Act
        var deleted = await job.RemoveExpiredSessionsAsync(ct);

        // Assert — the filter is ExpiresAt <= now, so the row expiring exactly on the instant goes too. Under the wall
        // clock all three would have been swept, the live one included.
        deleted.ShouldBe(2);
        (await this.RemainingKeysAsync(userId)).ShouldBe([liveKey]);
    }

    [Fact]
    public async Task RemoveExpiredSessions_DeletesBatchAfterBatchUntilNoneAreLeftAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — more expired rows than one batch deletes.
        var userId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 1001).Select(i => (NewKey(), ClockEpoch.AddMinutes(-1 - i))).ToArray();
        await this.SeedSessionsAsync(userId, rows);
        var job = this.CreateJob(new FakeTimeProvider(ClockEpoch));

        // Act
        var deleted = await job.RemoveExpiredSessionsAsync(ct);

        // Assert
        deleted.ShouldBe(1001);
        (await this.RemainingKeysAsync(userId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task HostedLoop_RemovesExpiredSessionsOnceTheIntervalHasPassedAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange
        var userId = Guid.NewGuid();
        await this.SeedSessionsAsync(userId, (NewKey(), ClockEpoch.AddMinutes(-1)));
        var clock = new FakeTimeProvider(ClockEpoch);
        using var job = this.CreateJob(clock, options => options.ExpiredSessionCleanup.Interval = TimeSpan.FromMinutes(10));
        await job.StartAsync(ct);

        try
        {
            // Nothing runs before the first interval has passed.
            (await this.RemainingKeysAsync(userId)).Count.ShouldBe(1);

            // Act
            clock.Advance(TimeSpan.FromMinutes(10));

            // Assert — the pass runs on the timer's thread, so wait for its effect.
            await WaitUntilAsync(async () => (await this.RemainingKeysAsync(userId)).Count == 0, ct);
        }
        finally
        {
            await job.StopAsync(ct);
        }
    }

    [Fact]
    public async Task HostedLoop_SwitchedOff_NeverRunsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var userId = Guid.NewGuid();
        await this.SeedSessionsAsync(userId, (NewKey(), ClockEpoch.AddMinutes(-1)));
        var clock = new FakeTimeProvider(ClockEpoch);
        using var job = this.CreateJob(clock, options => options.ExpiredSessionCleanup.Enabled = false);

        await job.StartAsync(ct);

        // A switched-off job ends without starting its timer, so its execution completes on its own.
        await job.ExecuteTask.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), ct);
        clock.Advance(TimeSpan.FromDays(1));
        await job.StopAsync(ct);

        (await this.RemainingKeysAsync(userId)).Count.ShouldBe(1);
    }

    private static string NewKey() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "the cleanup pass did not run");
            await Task.Delay(50, ct);
        }
    }

    private ExpiredSessionCleanupService<TestDbContext> CreateJob(TimeProvider clock, Action<BlueprintSessionOptions>? configure = null)
    {
        var options = new BlueprintSessionOptions { Lifetime = TimeSpan.FromHours(1) };
        configure?.Invoke(options);

        return new ExpiredSessionCleanupService<TestDbContext>(
            this.App.Services.GetRequiredService<IDbContextFactory<TestDbContext>>(),
            Options.Create(options),
            clock,
            NullLogger<ExpiredSessionCleanupService<TestDbContext>>.Instance);
    }

    private async Task SeedSessionsAsync(Guid userId, params (string SessionKey, DateTimeOffset ExpiresAt)[] sessions)
    {
        using var scope = this.App.CreateDbContextScope();
        scope.Context.ActiveSessions.AddRange(sessions.Select(s => new BlueprintActiveSession
        {
            UserId = userId,
            SessionKey = s.SessionKey,
            CreatedAt = s.ExpiresAt.AddHours(-1),
            ExpiresAt = s.ExpiresAt,
        }));
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> RemainingKeysAsync(Guid userId)
    {
        using var scope = this.App.CreateDbContextScope();
        return await scope.Context.ActiveSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.SessionKey)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
