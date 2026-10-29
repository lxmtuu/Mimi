namespace Mimi.Core.Playback;

/// <summary>
/// A no-repeats shuffle sequence: every item is returned once before a new
/// cycle begins. When possible, a new cycle also avoids starting with the
/// item that ended the previous cycle. Playback UI integration is planned
/// with the complete queue/shuffle experience in a later phase.
/// </summary>
public sealed class ShuffleBag<T>
{
    private readonly T[] items;
    private readonly Random random;
    private T[] cycle;
    private int cycleIndex;
    private bool hasLastItem;
    private T? lastItem;

    public ShuffleBag(IEnumerable<T> items, Random? random = null)
    {
        this.items = items.ToArray();
        this.random = random ?? Random.Shared;
        cycle = CreateShuffledCycle();
    }

    public int RemainingInCycle => cycle.Length - cycleIndex;

    public bool TryNext(out T? item)
    {
        if (items.Length == 0)
        {
            item = default;
            return false;
        }

        if (cycleIndex >= cycle.Length)
        {
            cycle = CreateShuffledCycle();
            cycleIndex = 0;
        }

        item = cycle[cycleIndex++];
        lastItem = item;
        hasLastItem = true;
        return true;
    }

    private T[] CreateShuffledCycle()
    {
        var shuffled = (T[])items.Clone();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
        }

        if (hasLastItem && shuffled.Length > 1 &&
            EqualityComparer<T>.Default.Equals(shuffled[0], lastItem))
        {
            var swapIndex = 1 + random.Next(shuffled.Length - 1);
            (shuffled[0], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[0]);
        }

        return shuffled;
    }
}
