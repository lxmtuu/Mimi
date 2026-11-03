using CommunityToolkit.Mvvm.ComponentModel;

namespace Mimi.ViewModels;

/// <summary>
/// One playback occurrence in the queue. Separate occurrences are important:
/// the same track can legitimately appear more than once when it is queued
/// manually or when Repeat All begins another cycle.
/// </summary>
public sealed partial class PlaybackQueueItemViewModel : ObservableObject
{
    public PlaybackQueueItemViewModel(TrackItemViewModel track) =>
        Track = track ?? throw new ArgumentNullException(nameof(track));

    public TrackItemViewModel Track { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionLabel))]
    private bool isCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionLabel))]
    private int queuePosition;

    public string PositionLabel => IsCurrent
        ? "▶"
        : QueuePosition > 0
            ? QueuePosition.ToString()
            : string.Empty;
}
