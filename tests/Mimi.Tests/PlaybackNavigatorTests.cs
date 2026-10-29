using Mimi.Core.Playback;

namespace Mimi.Tests;

public sealed class PlaybackNavigatorTests
{
    [Fact]
    public void NoCurrentTrack_ForwardStartsAtFirstItem() =>
        Assert.Equal(
            [0, 1, 2],
            PlaybackNavigator.GetCandidateIndices(3, -1, 1));

    [Fact]
    public void NoCurrentTrack_BackwardStartsAtLastItem() =>
        Assert.Equal(
            [2, 1, 0],
            PlaybackNavigator.GetCandidateIndices(3, -1, -1));

    [Fact]
    public void ForwardWrapsAroundVisibleList() =>
        Assert.Equal(
            [3, 0, 1, 2],
            PlaybackNavigator.GetCandidateIndices(4, 2, 1));

    [Fact]
    public void BackwardWrapsAroundVisibleList() =>
        Assert.Equal(
            [3, 2, 1, 0],
            PlaybackNavigator.GetCandidateIndices(4, 0, -1));

    [Fact]
    public void EmptyListHasNoCandidates() =>
        Assert.Empty(PlaybackNavigator.GetCandidateIndices(0, -1, 1));

    [Fact]
    public void OnlyDirectionOfStepMatters() =>
        Assert.Equal(
            [1, 2, 0],
            PlaybackNavigator.GetCandidateIndices(3, 0, 5));
}
