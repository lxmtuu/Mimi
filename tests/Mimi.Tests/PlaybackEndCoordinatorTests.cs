using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class PlaybackEndCoordinatorTests
{
    [Fact]
    public async Task ProcessAsync_AdvancesWhenGenerationRemainsCurrent()
    {
        var gate = new PlaybackEndNotificationGate();
        var coordinator = new PlaybackEndCoordinator(gate);
        var completionCalls = 0;
        var advanceCalls = 0;
        Assert.True(coordinator.TryQueue(4));

        var processed = await coordinator.ProcessAsync(
            4,
            () => 4,
            () =>
            {
                completionCalls++;
                return Task.CompletedTask;
            },
            () => advanceCalls++);

        Assert.True(processed);
        Assert.Equal(1, completionCalls);
        Assert.Equal(1, advanceCalls);
        Assert.False(gate.IsPending(4));
    }

    [Fact]
    public async Task ProcessAsync_DoesNotAdvanceWhenGenerationChangesDuringAwait()
    {
        var gate = new PlaybackEndNotificationGate();
        var coordinator = new PlaybackEndCoordinator(gate);
        var currentGeneration = 1L;
        var completionEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var advanceCalls = 0;
        Assert.True(coordinator.TryQueue(1));

        var processTask = coordinator.ProcessAsync(
            1,
            () => currentGeneration,
            async () =>
            {
                completionEntered.SetResult();
                await releaseCompletion.Task;
            },
            () => advanceCalls++);

        await completionEntered.Task;
        currentGeneration = 2;
        releaseCompletion.SetResult();

        Assert.False(await processTask);
        Assert.Equal(0, advanceCalls);
        Assert.False(gate.IsPending(1));
    }

    [Fact]
    public async Task ProcessAsync_StaleCallbackDoesNotConsumeNewerPendingCallback()
    {
        var gate = new PlaybackEndNotificationGate();
        var coordinator = new PlaybackEndCoordinator(gate);
        var currentGeneration = 1L;
        var completedGenerations = new List<long>();
        var advancedGenerations = new List<long>();

        Assert.True(coordinator.TryQueue(1));
        currentGeneration = 2;
        Assert.True(coordinator.TryQueue(2));

        var staleProcessed = await coordinator.ProcessAsync(
            1,
            () => currentGeneration,
            () =>
            {
                completedGenerations.Add(1);
                return Task.CompletedTask;
            },
            () => advancedGenerations.Add(1));

        Assert.False(staleProcessed);
        Assert.Empty(completedGenerations);
        Assert.Empty(advancedGenerations);
        Assert.False(gate.IsPending(1));
        Assert.True(gate.IsPending(2));

        var currentProcessed = await coordinator.ProcessAsync(
            2,
            () => currentGeneration,
            () =>
            {
                completedGenerations.Add(2);
                return Task.CompletedTask;
            },
            () => advancedGenerations.Add(2));

        Assert.True(currentProcessed);
        Assert.Equal([2L], completedGenerations);
        Assert.Equal([2L], advancedGenerations);
        Assert.False(gate.IsPending(2));
    }

    [Fact]
    public async Task ProcessAsync_ClearsPendingWhenCompletionThrows()
    {
        var gate = new PlaybackEndNotificationGate();
        var coordinator = new PlaybackEndCoordinator(gate);
        Assert.True(coordinator.TryQueue(9));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ProcessAsync(
                9,
                () => 9,
                () => throw new InvalidOperationException("completion failed"),
                () => { }));

        Assert.False(gate.IsPending(9));
    }
}
