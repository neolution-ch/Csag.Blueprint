namespace Csag.Blueprint.Application.UnitTests.Services;

using Csag.Blueprint.Application.Services;

/// <summary>
/// Pins the AsyncLocal semantics of <see cref="CurrentActorContext"/> that audit stamping relies on:
/// the actor label flows downward into awaited child work, but mutations made inside a child task
/// never flow back up into the caller's execution context.
/// </summary>
public sealed class CurrentActorContextTests
{
    [Fact]
    public void Current_WhenNothingSet_ReturnsNull()
    {
        CurrentActorContext.Current.ShouldBeNull();
    }

    [Fact]
    public void SetActor_ThenClear_RoundTripsThroughNull()
    {
        CurrentActorContext.SetActor("alice@example.com");
        CurrentActorContext.Current.ShouldBe("alice@example.com");

        CurrentActorContext.Clear();
        CurrentActorContext.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Current_FlowsIntoAwaitedChildAsyncMethod()
    {
        CurrentActorContext.SetActor("sa-background-worker");

        try
        {
            var observed = await ReadCurrentAfterYieldAsync();

            observed.ShouldBe("sa-background-worker");
        }
        finally
        {
            CurrentActorContext.Clear();
        }
    }

    [Fact]
    public async Task Clear_InsideChildTask_DoesNotClearTheParentView()
    {
        CurrentActorContext.SetActor("alice@example.com");

        try
        {
            await Task.Run(
                () =>
                {
                    CurrentActorContext.Clear();
                    CurrentActorContext.Current.ShouldBeNull();
                },
                TestContext.Current.CancellationToken);

            CurrentActorContext.Current.ShouldBe("alice@example.com");
        }
        finally
        {
            CurrentActorContext.Clear();
        }
    }

    [Fact]
    public async Task SetActor_InConcurrentTasks_IsIsolatedPerTask()
    {
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothReady = Task.WhenAll(firstReady.Task, secondReady.Task);

        // Both tasks set their actor before either one reads, so a leak in either direction would be observed.
        var firstObserved = SetAndObserveAfterRendezvousAsync("alice@example.com", firstReady, bothReady);
        var secondObserved = SetAndObserveAfterRendezvousAsync("sa-import-client", secondReady, bothReady);

        (await firstObserved).ShouldBe("alice@example.com");
        (await secondObserved).ShouldBe("sa-import-client");
        CurrentActorContext.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_AfterScopedWork_LeavesNoAmbientValueForSubsequentAwaitedWork()
    {
        CurrentActorContext.SetActor("alice@example.com");
        CurrentActorContext.Clear();

        var observed = await ReadCurrentAfterYieldAsync();

        observed.ShouldBeNull();
    }

    private static async Task<string?> ReadCurrentAfterYieldAsync()
    {
        await Task.Yield();
        return CurrentActorContext.Current;
    }

    private static async Task<string?> SetAndObserveAfterRendezvousAsync(string actor, TaskCompletionSource ready, Task allReady)
    {
        await Task.Yield();
        CurrentActorContext.SetActor(actor);
        ready.SetResult();
        await allReady;
        return CurrentActorContext.Current;
    }
}
