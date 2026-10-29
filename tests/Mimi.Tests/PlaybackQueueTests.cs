using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class PlaybackQueueTests
{
    [Fact]
    public void SequentialContext_UsesVisibleOrderForNextAndPrevious()
    {
        var a = Item("A");
        var b = Item("B");
        var c = Item("C");
        var d = Item("D");
        var queue = new PlaybackQueue<QueueItem>(new Random(42));

        queue.SetContext([a, b, c, d], b, shuffleEnabled: false);

        Assert.Equal(["A"], Names(queue.History));
        Assert.Equal(["C", "D"], Names(queue.GetNextCandidates()));
        Assert.True(queue.CommitNext(d));
        Assert.Equal(["A", "B"], Names(queue.History));
        Assert.Empty(queue.GetNextCandidates());
        Assert.Equal(["B", "A"], Names(queue.GetPreviousCandidates()));
        Assert.True(queue.CommitPrevious(b));
        Assert.Same(b, queue.Current);
        Assert.Equal(["D"], Names(queue.Upcoming));
    }

    [Fact]
    public void RepeatAll_GeneratesAnotherSequentialCycle()
    {
        var a = Item("A");
        var b = Item("B");
        var queue = new PlaybackQueue<QueueItem>(
            new Random(42),
            item => Item(item.Name),
            (left, right) => left.Name == right.Name)
        {
            RepeatMode = RepeatMode.All
        };

        queue.SetContext([a, b], b, shuffleEnabled: false);
        var candidates = queue.GetNextCandidates();

        Assert.Equal(["A", "B"], Names(candidates));
        Assert.NotSame(a, candidates[0]);
        Assert.True(queue.CommitNext(candidates[0]));
        Assert.Equal("A", queue.Current?.Name);
        Assert.Equal(["B"], Names(queue.Upcoming));
    }

    [Fact]
    public void ShuffleContext_ContainsEveryOtherItemOnceAndPreviousUsesActualOrder()
    {
        var items = Enumerable.Range(1, 8)
            .Select(index => Item(index.ToString()))
            .ToArray();
        var queue = new PlaybackQueue<QueueItem>(new Random(7));

        queue.SetContext(items, items[0], shuffleEnabled: true);
        var upcoming = queue.Upcoming.ToArray();

        Assert.Equal(
            items.Skip(1).Select(item => item.Name).Order(),
            Names(upcoming).Order());

        var firstNext = upcoming[0];
        var secondNext = upcoming[1];
        Assert.True(queue.CommitNext(firstNext));
        Assert.True(queue.CommitNext(secondNext));
        Assert.Equal(
            [firstNext.Name, items[0].Name],
            Names(queue.GetPreviousCandidates()));
    }

    [Fact]
    public void ShuffleRepeatAll_DoesNotStartNewCycleWithCurrentTrack()
    {
        var items = Enumerable.Range(1, 6)
            .Select(index => Item(index.ToString()))
            .ToArray();
        var queue = new PlaybackQueue<QueueItem>(
            new Random(19),
            item => Item(item.Name),
            (left, right) => left.Name == right.Name)
        {
            RepeatMode = RepeatMode.All
        };

        queue.SetContext(items, items[^1], shuffleEnabled: true);
        queue.ClearUpcoming();
        var candidates = queue.GetNextCandidates();

        Assert.Equal(6, candidates.Count);
        Assert.NotEqual(items[^1].Name, candidates[0].Name);
        Assert.Equal(
            items.Select(item => item.Name).Order(),
            Names(candidates).Order());
    }

    [Fact]
    public void DisablingShuffle_RestoresSourceOrderForUpcomingContextItems()
    {
        var a = Item("A");
        var b = Item("B");
        var c = Item("C");
        var d = Item("D");
        var manual = Item("Manual");
        var queue = new PlaybackQueue<QueueItem>(new Random(3));

        queue.SetContext([a, b, c, d], a, shuffleEnabled: true);
        queue.AddNext(manual);
        queue.SetShuffleEnabled(false);

        Assert.Equal(["Manual", "B", "C", "D"], Names(queue.Upcoming));
    }

    [Fact]
    public void ManualQueueOperations_MoveRemoveAndJump()
    {
        var a = Item("A");
        var b = Item("B");
        var c = Item("C");
        var x = Item("X");
        var y = Item("Y");
        var queue = new PlaybackQueue<QueueItem>(new Random(11));

        queue.SetContext([a, b, c], a, shuffleEnabled: false);
        queue.AddNext(x);
        queue.AddLast(y);
        Assert.Equal(["X", "B", "C", "Y"], Names(queue.Upcoming));
        Assert.True(queue.MoveUpcoming(y, 1));
        Assert.Equal(["X", "Y", "B", "C"], Names(queue.Upcoming));
        Assert.True(queue.JumpTo(b));
        Assert.Same(b, queue.Current);
        Assert.Equal(["A"], Names(queue.History));
        Assert.True(queue.RemoveUpcoming(c));
        Assert.Empty(queue.Upcoming);
    }

    [Fact]
    public void FailedReplacement_PreservesCurrentAsPreviousCandidate()
    {
        var a = Item("A");
        var b = Item("B");
        var queue = new PlaybackQueue<QueueItem>(new Random(5));

        queue.SetContext([a, b], a, shuffleEnabled: false);
        queue.MoveCurrentToHistory();

        Assert.Null(queue.Current);
        Assert.Equal(["A"], Names(queue.GetPreviousCandidates()));
        Assert.Equal(["B"], Names(queue.GetNextCandidates()));
    }

    [Fact]
    public void RestoreAndRemoveAll_KeepListsConsistent()
    {
        var a = Item("A");
        var b = Item("B");
        var c = Item("C");
        var queue = new PlaybackQueue<QueueItem>(new Random(13));

        queue.RestoreSnapshot(
            [a, b, c],
            [a],
            b,
            [c],
            shuffleEnabled: false);
        var removed = queue.RemoveAll(item => item.Name is "B" or "C");

        Assert.Equal(4, removed);
        Assert.Null(queue.Current);
        Assert.Equal(["A"], Names(queue.SourceItems));
        Assert.Equal(["A"], Names(queue.History));
        Assert.Empty(queue.Upcoming);
    }

    private static QueueItem Item(string name) => new(name);

    private static string[] Names(IEnumerable<QueueItem> items) =>
        items.Select(item => item.Name).ToArray();

    private sealed class QueueItem(string name)
    {
        public string Name { get; } = name;
    }
}
