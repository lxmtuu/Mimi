namespace Mimi.Core.Playback;

/// <summary>
/// Coordinates the asynchronous work that follows a natural track ending.
/// The event generation is checked both before the asynchronous completion
/// work and again after it finishes. That second check is the important one:
/// a user can load another track while playback history is being persisted,
/// and the old notification must not then repeat or advance the new track.
/// </summary>
public sealed class PlaybackEndCoordinator
{
    private readonly PlaybackEndNotificationGate gate;

    public PlaybackEndCoordinator()
        : this(new PlaybackEndNotificationGate())
    {
    }

    public PlaybackEndCoordinator(PlaybackEndNotificationGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        this.gate = gate;
    }

    public bool TryQueue(long playbackGeneration) =>
        gate.TryQueue(playbackGeneration);

    public void Complete(long playbackGeneration) =>
        gate.Complete(playbackGeneration);

    /// <returns>
    /// <see langword="true"/> when the generation remained current and
    /// <paramref name="advancePlayback"/> was invoked; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public async Task<bool> ProcessAsync(
        long eventGeneration,
        Func<long> currentGenerationProvider,
        Func<Task> completePlaybackAsync,
        Action advancePlayback)
    {
        ArgumentNullException.ThrowIfNull(currentGenerationProvider);
        ArgumentNullException.ThrowIfNull(completePlaybackAsync);
        ArgumentNullException.ThrowIfNull(advancePlayback);

        try
        {
            if (eventGeneration != currentGenerationProvider())
                return false;

            await completePlaybackAsync();

            if (eventGeneration != currentGenerationProvider())
                return false;

            advancePlayback();
            return true;
        }
        finally
        {
            gate.Complete(eventGeneration);
        }
    }
}
