using CommunityToolkit.Mvvm.ComponentModel;
using Mimi.Core.Text;
using Mimi.Localization;
using Mimi.Models;
using Mimi.Services;

namespace Mimi.ViewModels;

/// <summary>Presentation state for one row in the track list.</summary>
public sealed partial class TrackItemViewModel : ObservableObject
{
    private readonly LocalizationService loc;

    public TrackItemViewModel(
        Track track,
        bool isFavorite,
        LocalizationService? localizationService = null)
    {
        loc = localizationService ?? new LocalizationService();
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
        ? loc["Metadata_UnknownArtist"]
        : Track.Artist;

    public string Album => string.IsNullOrWhiteSpace(Track.Album)
        ? loc["Metadata_UnknownAlbum"]
        : Track.Album;

    public string AlbumArtist => string.IsNullOrWhiteSpace(Track.AlbumArtist)
        ? Artist
        : Track.AlbumArtist;

    public string Genre => string.IsNullOrWhiteSpace(Track.Genre)
        ? loc["Metadata_UnknownGenre"]
        : Track.Genre;

    public string YearLabel => Track.Year?.ToString() ?? string.Empty;

    public string DurationLabel => Track.Duration is { } duration &&
                                    duration > TimeSpan.Zero
        ? FormatDuration(duration)
        : "--:--";

    public double DurationSortSeconds =>
        Track.Duration?.TotalSeconds ?? -1;

    public int YearSortValue => Track.Year ?? -1;

    public string BitrateLabel => Track.BitrateKbps is { } bitrate && bitrate > 0
        ? $"{bitrate} kbps"
        : string.Empty;

    public string Format => Track.Format;

    public string FilePath => Track.FilePath;

    public string? CoverArtPath => Track.CoverArtPath;

    public string? CoverThumbnailPath =>
        CoverArtPaths.GetThumbnailPath(Track.CoverArtPath);

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
                : loc.Format("Metadata_LocalMusicSuffix", Format);
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
    [NotifyPropertyChangedFor(nameof(CurrentPlaybackIndicatorLabel))]
    private bool isCurrentTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPlaybackIndicatorLabel))]
    private bool isCurrentTrackPlaying;

    [ObservableProperty]
    private DateTimeOffset? lastPlayedAtUtc;

    [ObservableProperty]
    private int playCount;

    [ObservableProperty]
    private int playlistPosition = int.MaxValue;

    [ObservableProperty]
    private int searchRank = int.MaxValue;

    public string FavoriteGlyph => IsFavorite ? "♥" : "♡";

    public string FavoriteToolTip => IsFavorite
        ? loc["Library_RemoveFromFavorites"]
        : loc["Library_AddToFavorites"];

    public string PlaybackStatus => HasPlaybackError ? "⚠" : string.Empty;

    public string CurrentPlaybackIndicatorLabel => IsCurrentTrackPlaying
        ? loc["Playback_NowPlayingIndicator"]
        : IsCurrentTrack
            ? loc["Playback_PausedIndicator"]
            : string.Empty;

    internal void SetCurrentPlaybackState(
        bool isCurrentTrack,
        bool isPlaying)
    {
        IsCurrentTrack = isCurrentTrack;
        IsCurrentTrackPlaying = isCurrentTrack && isPlaying;
    }

    internal void RefreshLocalizedLabels()
    {
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(Album));
        OnPropertyChanged(nameof(AlbumArtist));
        OnPropertyChanged(nameof(Genre));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(FavoriteToolTip));
        OnPropertyChanged(nameof(CurrentPlaybackIndicatorLabel));
    }

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
        OnPropertyChanged(nameof(AlbumArtist));
        OnPropertyChanged(nameof(Genre));
        OnPropertyChanged(nameof(YearLabel));
        OnPropertyChanged(nameof(YearSortValue));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(DurationSortSeconds));
        OnPropertyChanged(nameof(BitrateLabel));
        OnPropertyChanged(nameof(CoverArtPath));
        OnPropertyChanged(nameof(CoverThumbnailPath));
        OnPropertyChanged(nameof(Subtitle));
    }

    internal void UpdateCoverArtPath(string? coverArtPath)
    {
        if (string.Equals(
                Track.CoverArtPath,
                coverArtPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Track.CoverArtPath = coverArtPath;
        OnPropertyChanged(nameof(CoverArtPath));
        OnPropertyChanged(nameof(CoverThumbnailPath));
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
