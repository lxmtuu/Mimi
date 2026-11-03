using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class PlaybackStopClassifierTests
{
    [Theory]
    [InlineData(false, 1L, true)]
    [InlineData(false, long.MaxValue, true)]
    [InlineData(false, 0L, false)]
    [InlineData(false, -1L, false)]
    [InlineData(false, null, false)]
    [InlineData(true, 100L, false)]
    [InlineData(true, null, false)]
    public void IsNaturalEnd_ClassifiesExceptionAndLoadedReader(
        bool hasException,
        long? readerLength,
        bool expected)
    {
        Assert.Equal(
            expected,
            PlaybackStopClassifier.IsNaturalEnd(hasException, readerLength));
    }
}
