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
        this.isFavorite = isFavorite || track.IsFavorite;
        track.IsFavorite = this.isFavorite;
        hasPlaybackError = track.HasError || track.IsMissing;
        lastPlayedAtUtc = track.LastPlayedAtUtc;
        playCount = Math.Max(0, track.PlayCount);
        RefreshNormalizedSearchText();
    }

    public Track Track { get; }

    public string Title => Track.Title;

    public string Artist => string.IsNullOrWhiteSpace(Track.Artist)
        ? "Unknown artist"
        : Track.Artist;

    public string Album => string.IsNullOrWhiteSpace(Track.Album)
        ? "Unknown album"
        : Track.Album;

    public string Genre => string.IsNullOrWhiteSpace(Track.Genre)
        ? "Unknown genre"
        : Track.Genre;

    public string YearLabel => Track.Year?.ToString() ?? string.Empty;

    public string DurationLabel => Track.Duration is { } duration &&
                                    duration > TimeSpan.Zero
        ? FormatDuration(duration)
        : "--:--";

    public string BitrateLabel => Track.BitrateKbps is { } bitrate && bitrate > 0
        ? $"{bitrate} kbps"
        : string.Empty;

    public string Format => Track.Format;

    public string FilePath => Track.FilePath;

    public string? CoverArtPath => Track.CoverArtPath;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Track.Artist)) parts.Add(Track.Artist);
            if (!string.IsNullOrWhiteSpace(Track.Album)) parts.Add(Track.Album);
            if (Format.Length > 0) parts.Add(Format);

            return parts.Count > 1
                ? string.Join("  •  ", parts)
                : $"{Format}  •  Local music";
        }
    }

    internal string NormalizedTitle { get; private set; } = string.Empty;

    internal string NormalizedFormat { get; private set; } = string.Empty;

    internal string NormalizedSearchText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteToolTip))]
    private bool isFavorite;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackStatus))]
    private bool hasPlaybackError;

    [ObservableProperty]
    private DateTimeOffset? lastPlayedAtUtc;

    [ObservableProperty]
    private int playCount;

    public string FavoriteGlyph => IsFavorite ? "♥" : "♡";

    public string FavoriteToolTip => IsFavorite
        ? "Remove from favorites"
        : "Add to favorites";

    public string PlaybackStatus => HasPlaybackError ? "⚠" : string.Empty;

    public void UpdateMetadata(Track updated)
    {
        if (!string.Equals(
                Track.FilePath,
                updated.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (updated.Id > 0) Track.Id = updated.Id;
        Track.MetadataTitle = updated.MetadataTitle;
        Track.Artist = updated.Artist;
        Track.Album = updated.Album;
        Track.AlbumArtist = updated.AlbumArtist;
        Track.Genre = updated.Genre;
        Track.Year = updated.Year;
        Track.Duration = updated.Duration;
        Track.BitrateKbps = updated.BitrateKbps;
        Track.SampleRateHz = updated.SampleRateHz;
        Track.FileSizeBytes = updated.FileSizeBytes;
        Track.FileModifiedUtc = updated.FileModifiedUtc;
        Track.IsMissing = updated.IsMissing;
        Track.HasError = updated.HasError;
        Track.CoverArtPath = updated.CoverArtPath;
        Track.MetadataSource = updated.MetadataSource;

        RefreshNormalizedSearchText();
        HasPlaybackError = Track.HasError || Track.IsMissing;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(Album));
        OnPropertyChanged(nameof(Genre));
        OnPropertyChanged(nameof(YearLabel));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(BitrateLabel));
        OnPropertyChanged(nameof(CoverArtPath));
        OnPropertyChanged(nameof(Subtitle));
    }

    public void MarkPlayed(DateTimeOffset playedAtUtc)
    {
        Track.LastPlayedAtUtc = playedAtUtc;
        Track.PlayCount = Math.Max(0, Track.PlayCount) + 1;
        Track.IsMissing = false;
        Track.HasError = false;

        LastPlayedAtUtc = playedAtUtc;
        PlayCount = Track.PlayCount;
        HasPlaybackError = false;
    }

    private void RefreshNormalizedSearchText()
    {
        NormalizedTitle = TextSearch.Normalize(Track.Title);
        NormalizedFormat = TextSearch.Normalize(Track.Format);
        NormalizedSearchText = TextSearch.Normalize(string.Join(
            " ",
            Track.Title,
            Track.Artist,
            Track.Album,
            Track.AlbumArtist,
            Track.Genre,
            Track.Format));
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
}
