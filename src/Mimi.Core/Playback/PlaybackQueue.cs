namespace Mimi.Core.Playback;

public enum RepeatMode
{
    Off,
    All,
    One
}

/// <summary>
/// The single source of truth for sequential, shuffled, and manually queued
/// playback. The queue keeps the original context (for example, the visible
/// playlist), tracks that have actually been played, the current track, and
/// the explicit upcoming sequence. Load attempts do not mutate the queue;
/// callers commit a candidate only after playback starts successfully.
/// </summary>
public sealed class PlaybackQueue<T> where T : class
{
    private List<T> sourceItems = [];
    private readonly List<T> history = [];
    private readonly List<T> upcoming = [];
    private readonly Random random;
    private readonly Func<T, T> cycleItemFactory;
    private readonly Func<T, T, bool> representsSameItem;
    private IReadOnlyList<T>? pendingGeneratedNext;
    private RepeatMode repeatMode;

    public PlaybackQueue(
        Random? random = null,
        Func<T, T>? cycleItemFactory = null,
        Func<T, T, bool>? representsSameItem = null)
    {
        this.random = random ?? Random.Shared;
        this.cycleItemFactory = cycleItemFactory ?? (item => item);
        this.representsSameItem =
            representsSameItem ?? ((left, right) => ReferenceEquals(left, right));
    }

    public IReadOnlyList<T> SourceItems => sourceItems;

    public IReadOnlyList<T> History => history;

    public IReadOnlyList<T> Upcoming => upcoming;

    public T? Current { get; private set; }

    public bool IsShuffleEnabled { get; private set; }

    public RepeatMode RepeatMode
    {
        get => repeatMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            repeatMode = value;
            pendingGeneratedNext = null;
        }
    }

    public void SetContext(
        IEnumerable<T> items,
        T current,
        bool shuffleEnabled)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(current);

        sourceItems.Clear();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (IndexOfReference(sourceItems, item) < 0)
                sourceItems.Add(item);
        }

        var currentIndex = IndexOfReference(sourceItems, current);
        if (currentIndex < 0)
        {
            sourceItems.Add(current);
            currentIndex = sourceItems.Count - 1;
        }

        Current = current;
        IsShuffleEnabled = shuffleEnabled;
        pendingGeneratedNext = null;
        history.Clear();
        upcoming.Clear();

        if (shuffleEnabled)
        {
            var remaining = sourceItems
                .Where(item => !ReferenceEquals(item, current))
                .ToList();
            upcoming.AddRange(Shuffled(remaining, avoidFirst: null));
        }
        else
        {
            history.AddRange(sourceItems.Take(currentIndex));
            upcoming.AddRange(sourceItems.Skip(currentIndex + 1));
        }
    }

    public void RestoreSnapshot(
        IEnumerable<T> source,
        IEnumerable<T> playedHistory,
        T current,
        IEnumerable<T> upcomingItems,
        bool shuffleEnabled)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(playedHistory);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(upcomingItems);

        sourceItems.Clear();
        foreach (var item in source)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (IndexOfReference(sourceItems, item) < 0)
                sourceItems.Add(item);
        }

        if (IndexOfReference(sourceItems, current) < 0)
            sourceItems.Add(current);

        Current = current;
        IsShuffleEnabled = shuffleEnabled;
        pendingGeneratedNext = null;

        history.Clear();
        history.AddRange(playedHistory.Where(item =>
            item is not null && !ReferenceEquals(item, current)));

        upcoming.Clear();
        upcoming.AddRange(upcomingItems.Where(item =>
            item is not null && !ReferenceEquals(item, current)));
    }

    public void SetShuffleEnabled(bool value)
    {
        if (IsShuffleEnabled == value) return;

        IsShuffleEnabled = value;
        pendingGeneratedNext = null;
        if (upcoming.Count < 2) return;

        List<T> reordered;
        if (value)
        {
            reordered = Shuffled(upcoming, avoidFirst: null);
        }
        else
        {
            var manuallyQueued = upcoming
                .Where(item => IndexOfReference(sourceItems, item) < 0)
                .ToList();
            var contextItems = upcoming
                .Where(item => IndexOfReference(sourceItems, item) >= 0)
                .OrderBy(item => IndexOfReference(sourceItems, item))
                .ToList();
            reordered = [.. manuallyQueued, .. contextItems];
        }

        upcoming.Clear();
        upcoming.AddRange(reordered);
    }

    public IReadOnlyList<T> GetNextCandidates()
    {
        if (upcoming.Count > 0)
        {
            pendingGeneratedNext = null;
            return [.. upcoming];
        }

        if (Current is null)
        {
            if (sourceItems.Count == 0) return [];

            pendingGeneratedNext = IsShuffleEnabled
                ? Shuffled(sourceItems, avoidFirst: null)
                : [.. sourceItems];
            return pendingGeneratedNext;
        }

        if (RepeatMode != RepeatMode.All) return [];

        if (sourceItems.Count == 0)
        {
            pendingGeneratedNext = [cycleItemFactory(Current)];
            return pendingGeneratedNext;
        }

        var nextCycle = IsShuffleEnabled
            ? Shuffled(sourceItems, avoidFirst: Current)
            : [.. sourceItems];
        pendingGeneratedNext = nextCycle
            .Select(item => cycleItemFactory(item))
            .ToArray();
        return pendingGeneratedNext;
    }

    public bool CommitNext(T candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (ReferenceEquals(candidate, Current))
        {
            if (pendingGeneratedNext is not null)
            {
                var generatedIndex = IndexOfReference(
                    pendingGeneratedNext,
                    candidate);
                if (generatedIndex < 0) return false;

                upcoming.Clear();
                upcoming.AddRange(pendingGeneratedNext.Skip(generatedIndex + 1));
                pendingGeneratedNext = null;
            }

            return true;
        }

        var upcomingIndex = IndexOfReference(upcoming, candidate);
        if (upcomingIndex >= 0)
        {
            if (Current is not null)
                history.Add(Current);

            upcoming.RemoveRange(0, upcomingIndex + 1);
            Current = candidate;
            pendingGeneratedNext = null;
            return true;
        }

        if (pendingGeneratedNext is null) return false;

        var generatedCandidateIndex = IndexOfReference(
            pendingGeneratedNext,
            candidate);
        if (generatedCandidateIndex < 0) return false;

        if (Current is not null)
            history.Add(Current);

        upcoming.Clear();
        upcoming.AddRange(
            pendingGeneratedNext.Skip(generatedCandidateIndex + 1));
        Current = candidate;
        pendingGeneratedNext = null;
        return true;
    }

    public IReadOnlyList<T> GetPreviousCandidates() =>
        ((IEnumerable<T>)history).Reverse().ToArray();

    public bool CommitPrevious(T candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var historyIndex = IndexOfReference(history, candidate);
        if (historyIndex < 0) return false;

        var redo = history.Skip(historyIndex + 1).ToList();
        history.RemoveRange(historyIndex, history.Count - historyIndex);

        if (Current is not null)
            redo.Add(Current);

        upcoming.InsertRange(0, redo);
        Current = candidate;
        pendingGeneratedNext = null;
        return true;
    }

    public bool JumpTo(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ReferenceEquals(item, Current)) return true;
        if (IndexOfReference(upcoming, item) >= 0) return CommitNext(item);
        if (IndexOfReference(history, item) >= 0) return CommitPrevious(item);
        return false;
    }

    public void AddNext(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        upcoming.Insert(0, item);
        pendingGeneratedNext = null;
    }

    public void AddNextRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var additions = items.ToArray();
        foreach (var item in additions)
            ArgumentNullException.ThrowIfNull(item);

        upcoming.InsertRange(0, additions);
        pendingGeneratedNext = null;
    }

    public void AddLast(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        upcoming.Add(item);
        pendingGeneratedNext = null;
    }

    public void AddLastRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var additions = items.ToArray();
        foreach (var item in additions)
            ArgumentNullException.ThrowIfNull(item);

        upcoming.AddRange(additions);
        pendingGeneratedNext = null;
    }

    public bool MoveUpcoming(T item, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sourceIndex = IndexOfReference(upcoming, item);
        if (sourceIndex < 0) return false;

        targetIndex = Math.Clamp(targetIndex, 0, upcoming.Count - 1);
        if (targetIndex == sourceIndex) return true;

        upcoming.RemoveAt(sourceIndex);
        upcoming.Insert(targetIndex, item);
        pendingGeneratedNext = null;
        return true;
    }

    public bool RemoveUpcoming(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = IndexOfReference(upcoming, item);
        if (index < 0) return false;

        upcoming.RemoveAt(index);
        pendingGeneratedNext = null;
        return true;
    }

    public void ClearUpcoming()
    {
        upcoming.Clear();
        pendingGeneratedNext = null;
    }

    /// <summary>
    /// Preserves a track in history when its replacement failed to load. The
    /// audio engine has already stopped it, but Previous should still be
    /// able to return to the last successfully played track.
    /// </summary>
    public void MoveCurrentToHistory()
    {
        if (Current is null) return;

        history.Add(Current);
        Current = null;
        pendingGeneratedNext = null;
    }

    public int RemoveAll(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var removed = sourceItems.RemoveAll(item => predicate(item));
        removed += history.RemoveAll(item => predicate(item));
        removed += upcoming.RemoveAll(item => predicate(item));

        if (Current is not null && predicate(Current))
        {
            Current = null;
            removed++;
        }

        pendingGeneratedNext = null;
        return removed;
    }

    public void Clear()
    {
        sourceItems.Clear();
        history.Clear();
        upcoming.Clear();
        Current = null;
        pendingGeneratedNext = null;
    }

    private List<T> Shuffled(IReadOnlyCollection<T> items, T? avoidFirst)
    {
        if (items.Count < 2) return [.. items];

        var bag = new ShuffleBag<T>(items, random);
        var shuffled = new List<T>(items.Count);
        while (bag.TryNext(out var item) && shuffled.Count < items.Count)
            shuffled.Add(item!);

        if (avoidFirst is not null &&
            representsSameItem(shuffled[0], avoidFirst))
        {
            var swapIndex = 1 + random.Next(shuffled.Count - 1);
            (shuffled[0], shuffled[swapIndex]) =
                (shuffled[swapIndex], shuffled[0]);
        }

        return shuffled;
    }

    private static int IndexOfReference(
        IReadOnlyList<T> items,
        T target)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
                return i;
        }

        return -1;
    }
}
