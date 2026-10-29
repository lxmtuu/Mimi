namespace Mimi.Core.Playback;

/// <summary>
/// Pure index calculations for sequential next/previous navigation in the
/// currently visible track list. Keeping this logic in Core makes wrapping,
/// reverse traversal, and broken-track skipping unit-testable.
/// </summary>
public static class PlaybackNavigator
{
    public static IReadOnlyList<int> GetCandidateIndices(
        int itemCount,
        int currentIndex,
        int step)
    {
        if (itemCount <= 0) return [];

        var direction = step < 0 ? -1 : 1;
        var candidates = new int[itemCount];

        if (currentIndex < 0 || currentIndex >= itemCount)
        {
            for (var i = 0; i < itemCount; i++)
                candidates[i] = direction > 0 ? i : itemCount - 1 - i;
            return candidates;
        }

        for (var attempt = 0; attempt < itemCount; attempt++)
        {
            var raw = currentIndex + direction * (attempt + 1);
            candidates[attempt] = ((raw % itemCount) + itemCount) % itemCount;
        }

        return candidates;
    }
}
