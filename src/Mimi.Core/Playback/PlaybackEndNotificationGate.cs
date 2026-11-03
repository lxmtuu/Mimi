namespace Mimi.Core.Playback;

/// <summary>
/// Tracks playback-end notifications that have been accepted for processing.
/// Notifications are keyed by playback generation: duplicates for one
/// generation coalesce, while completing an older generation can never clear
/// a newer generation's pending notification.
/// </summary>
public sealed class PlaybackEndNotificationGate
{
    private readonly object syncRoot = new();
    private readonly HashSet<long> pendingGenerations = [];

    /// <summary>
    /// Marks a generation as pending. Returns <see langword="false"/> when
    /// that same generation is already pending.
    /// </summary>
    public bool TryQueue(long playbackGeneration)
    {
        lock (syncRoot)
            return pendingGenerations.Add(playbackGeneration);
    }

    public bool IsPending(long playbackGeneration)
    {
        lock (syncRoot)
            return pendingGenerations.Contains(playbackGeneration);
    }

    /// <summary>
    /// Releases only the supplied generation. Completing a stale generation
    /// deliberately leaves every newer pending notification untouched.
    /// </summary>
    public void Complete(long playbackGeneration)
    {
        lock (syncRoot)
            pendingGenerations.Remove(playbackGeneration);
    }

    public int PendingCount
    {
        get
        {
            lock (syncRoot)
                return pendingGenerations.Count;
        }
    }
}
