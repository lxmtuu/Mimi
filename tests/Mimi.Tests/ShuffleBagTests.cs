using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class ShuffleBagTests
{
    [Fact]
    public void EachCycleContainsEveryItemExactlyOnce()
    {
        var bag = new ShuffleBag<int>(
            Enumerable.Range(1, 8),
            new Random(42));

        var firstCycle = Take(bag, 8);
        var secondCycle = Take(bag, 8);

        Assert.Equal(Enumerable.Range(1, 8), firstCycle.Order());
        Assert.Equal(Enumerable.Range(1, 8), secondCycle.Order());
        Assert.NotEqual(firstCycle[^1], secondCycle[0]);
    }

    [Fact]
    public void EmptyBagReturnsFalse()
    {
        var bag = new ShuffleBag<int>([]);

        Assert.False(bag.TryNext(out var item));
        Assert.Equal(0, item);
    }

    [Fact]
    public void SingleItemCanRepeatAcrossCycles()
    {
        var bag = new ShuffleBag<string>(["only"], new Random(1));

        Assert.Equal(["only", "only", "only"], Take(bag, 3));
    }

    private static List<T> Take<T>(ShuffleBag<T> bag, int count)
    {
        var result = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            Assert.True(bag.TryNext(out var item));
            result.Add(item!);
        }

        return result;
    }
}
