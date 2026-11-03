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

    [Theory]
    [InlineData(false, null, true, true)]
    [InlineData(false, 0L, true, true)]
    [InlineData(false, null, false, false)]
    [InlineData(true, null, true, false)]
    public void IsNaturalEnd_AcceptsAReportedEndWhenAStreamHasNoLength(
        bool hasException,
        long? readerLength,
        bool reachedReportedEnd,
        bool expected)
    {
        // Streamed online tracks frequently expose no usable length, so the
        // decoded position reaching the provider duration is what marks the
        // end. Without it the output stop looked like an interruption.
        Assert.Equal(
            expected,
            PlaybackStopClassifier.IsNaturalEnd(
                hasException,
                readerLength,
                reachedReportedEnd));
    }
}
