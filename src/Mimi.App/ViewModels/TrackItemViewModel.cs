using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Core.Text;
using Mimi.Models;

namespace Mimi.ViewModels;

/// <summary>Presentation state for one row in the track list.</summary>
public sealed partial class TrackItemViewModel : ObservableObject
{
    public TrackItemViewModel(Track track, bool isFavorite)
    {
        Track = track;
        this.isFavorite = isFavorite;
        NormalizedTitle = TextSearch.Normalize(track.Title);
        NormalizedFormat = TextSearch.Normalize(track.Format);
    }

    public Track Track { get; }

    public string Title => Track.Title;

    public string Format => Track.Format;

    public string FilePath => Track.FilePath;

    internal string NormalizedTitle { get; }

    internal string NormalizedFormat { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteToolTip))]
    private bool isFavorite;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackStatus))]
    private bool hasPlaybackError;

    public string FavoriteGlyph => IsFavorite ? "♥" : "♡";

    public string FavoriteToolTip => IsFavorite
        ? "Remove from favorites"
        : "Add to favorites";

    public string PlaybackStatus => HasPlaybackError ? "⚠" : string.Empty;
}
