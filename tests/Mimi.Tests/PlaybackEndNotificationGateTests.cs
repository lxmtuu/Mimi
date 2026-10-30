using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class PlaybackEndNotificationGateTests
{
    [Fact]
    public void TryQueue_CoalescesDuplicatesForSameGeneration()
    {
        var gate = new PlaybackEndNotificationGate();

        Assert.True(gate.TryQueue(7));
        Assert.False(gate.TryQueue(7));
        Assert.True(gate.IsPending(7));
        Assert.Equal(1, gate.PendingCount);
    }

    [Fact]
    public void TryQueue_AllowsNewGenerationWhileOldGenerationIsPending()
    {
        var gate = new PlaybackEndNotificationGate();

        Assert.True(gate.TryQueue(1));
        Assert.True(gate.TryQueue(2));

        Assert.True(gate.IsPending(1));
        Assert.True(gate.IsPending(2));
        Assert.Equal(2, gate.PendingCount);
    }

    [Fact]
    public void Complete_OldGenerationDoesNotClearNewGeneration()
    {
        var gate = new PlaybackEndNotificationGate();
        gate.TryQueue(1);
        gate.TryQueue(2);

        gate.Complete(1);

        Assert.False(gate.IsPending(1));
        Assert.True(gate.IsPending(2));
    }

    [Fact]
    public void Complete_CurrentGenerationAllowsItToBeQueuedAgain()
    {
        var gate = new PlaybackEndNotificationGate();

        Assert.True(gate.TryQueue(3));
        gate.Complete(3);

        Assert.True(gate.TryQueue(3));
    }

    [Fact]
    public async Task TryQueue_ConcurrentDuplicatesOnlyAcceptOne()
    {
        var gate = new PlaybackEndNotificationGate();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() => gate.TryQueue(11))));

        Assert.Single(results, accepted => accepted);
        Assert.Equal(1, gate.PendingCount);
    }
}
