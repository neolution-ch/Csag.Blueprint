namespace Csag.Blueprint.Application.UnitTests.Services;

using Csag.Blueprint.Application.Services;

/// <summary>
/// Pins the AsyncLocal semantics of <see cref="TenantContext"/> that tenant isolation relies on:
/// the value flows downward into awaited child work, but mutations made inside a child task never
/// flow back up into the caller's execution context.
/// </summary>
public sealed class TenantContextTests
{
    [Fact]
    public void Current_WhenNothingSet_ReturnsNull()
    {
        TenantContext.Current.ShouldBeNull();
    }

    [Fact]
    public void SetTenant_ThenClear_RoundTripsThroughNull()
    {
        var tenantId = Guid.NewGuid();

        TenantContext.SetTenant(tenantId);
        TenantContext.Current.ShouldBe(tenantId);

        TenantContext.Clear();
        TenantContext.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Current_FlowsIntoAwaitedChildAsyncMethod()
    {
        var tenantId = Guid.NewGuid();
        TenantContext.SetTenant(tenantId);

        try
        {
            var observed = await ReadCurrentAfterYieldAsync();

            observed.ShouldBe(tenantId);
        }
        finally
        {
            TenantContext.Clear();
        }
    }

    [Fact]
    public async Task Clear_InsideChildTask_DoesNotClearTheParentView()
    {
        var tenantId = Guid.NewGuid();
        TenantContext.SetTenant(tenantId);

        try
        {
            await Task.Run(
                () =>
                {
                    TenantContext.Clear();
                    TenantContext.Current.ShouldBeNull();
                },
                TestContext.Current.CancellationToken);

            TenantContext.Current.ShouldBe(tenantId);
        }
        finally
        {
            TenantContext.Clear();
        }
    }

    [Fact]
    public async Task SetTenant_InConcurrentTasks_IsIsolatedPerTask()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothReady = Task.WhenAll(firstReady.Task, secondReady.Task);

        // Both tasks set their tenant before either one reads, so a leak in either direction would be observed.
        var firstObserved = SetAndObserveAfterRendezvousAsync(firstId, firstReady, bothReady);
        var secondObserved = SetAndObserveAfterRendezvousAsync(secondId, secondReady, bothReady);

        (await firstObserved).ShouldBe(firstId);
        (await secondObserved).ShouldBe(secondId);
        TenantContext.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_AfterScopedWork_LeavesNoAmbientValueForSubsequentAwaitedWork()
    {
        TenantContext.SetTenant(Guid.NewGuid());
        TenantContext.Clear();

        var observed = await ReadCurrentAfterYieldAsync();

        observed.ShouldBeNull();
    }

    private static async Task<Guid?> ReadCurrentAfterYieldAsync()
    {
        await Task.Yield();
        return TenantContext.Current;
    }

    private static async Task<Guid?> SetAndObserveAfterRendezvousAsync(Guid tenantId, TaskCompletionSource ready, Task allReady)
    {
        await Task.Yield();
        TenantContext.SetTenant(tenantId);
        ready.SetResult();
        await allReady;
        return TenantContext.Current;
    }
}
