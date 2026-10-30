namespace Mimi.Core.Playback;

/// <summary>
/// Identifies the playback run that reached its natural end. The generation
/// is captured by the audio engine when the stop is classified, before the
/// event is marshalled to the UI thread, so a delayed notification cannot be
/// mistaken for the end of a track loaded in the meantime.
/// </summary>
public sealed class PlaybackEndedEventArgs : EventArgs
{
    public PlaybackEndedEventArgs(long playbackGeneration)
    {
        PlaybackGeneration = playbackGeneration;
    }

    public long PlaybackGeneration { get; }
}
