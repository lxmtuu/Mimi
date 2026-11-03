using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.YouTube;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// YouTube search inside Mimi. Mimi only receives metadata from the official
/// Data API; the chosen video plays in YouTube's own embedded player, which
/// stays visible in the Now Playing pane (and, later, on Stage). Closing that
/// player stops playback — Mimi never keeps YouTube audio running without a
/// visible player.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private enum YouTubeSearchStatus
    {
        NotLoaded,
        Loading,
        Ready,
        Empty,
        Failed,
        QuotaExceeded,
        InvalidKey,
        NotConfigured,
        Unavailable
    }

    private readonly IYouTubeSearchService? youtubeSearchService;
    private CancellationTokenSource? youtubeSearchCancellation;
    private YouTubeSearchStatus youtubeSearchStatus = YouTubeSearchStatus.NotLoaded;
    private int youtubeSearchResultCount;
    private int youtubeSearchVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsYouTubeContentVisible))]
    private bool isYouTubeOpen;

    [ObservableProperty]
    private string youTubeSearchText = string.Empty;

    [ObservableProperty]
    private bool isYouTubeLoading;

    [ObservableProperty]
    private string youTubeStatusLabel = string.Empty;

    [ObservableProperty]
    private YouTubeVideoItemViewModel? selectedYouTubeVideo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsYouTubePlaying))]
    [NotifyPropertyChangedFor(nameof(IsVisualizerSurfaceVisible))]
    [NotifyPropertyChangedFor(nameof(HasCurrentPlaybackSource))]
    private YouTubeVideoItemViewModel? currentYouTubeVideo;

    public ObservableCollection<YouTubeVideoItemViewModel> YouTubeVideos { get; } = [];

    public bool IsYouTubeServiceInstalled => youtubeSearchService is not null;

    /// <summary>
    /// True once a user-supplied API key is present. Without one the feature
    /// stays hidden rather than failing at search time.
    /// </summary>
    public bool IsYouTubeConfigured => youtubeSearchService?.IsConfigured ?? false;

    public bool IsYouTubeAvailable => YouTubeSearchEnabled && IsYouTubeConfigured;

    public bool IsYouTubeContentVisible => IsYouTubeOpen && !IsSettingsOpen;

    public bool IsYouTubePlaying => CurrentYouTubeVideo is not null;

    /// <summary>
    /// The visualizer and the embedded player share one frame: while a video
    /// plays, the visualizer is hidden instead of animating behind an opaque
    /// browser surface.
    /// </summary>
    public bool IsVisualizerSurfaceVisible => !IsYouTubePlaying;

    public string YouTubeKeyFilePath =>
        youtubeSearchService?.KeyFilePath ?? string.Empty;

    /// <summary>
    /// Re-reads whether a key is present. The key file is resolved lazily, so
    /// dropping youtube-api-key.txt into the data folder takes effect as soon
    /// as this runs: no restart and no stale hidden nav entry.
    /// </summary>
    public void RefreshYouTubeAvailability()
    {
        if (disposed) return;

        OnPropertyChanged(nameof(IsYouTubeConfigured));
        OnPropertyChanged(nameof(IsYouTubeAvailable));

        if (IsYouTubeAvailable)
        {
            // A newly valid key must not leave the old "not configured" text.
            RefreshYouTubeStatusLabel();
            return;
        }

        // Losing the key hides the surface, so nothing may keep playing on it.
        IsYouTubeOpen = false;
        StopYouTubeVideo();
    }

    partial void OnCurrentYouTubeVideoChanged(YouTubeVideoItemViewModel? value) =>
        RefreshYouTubePlaybackStates();

    [RelayCommand]
    private void ShowYouTube()
    {
        if (!IsYouTubeAvailable)
        {
            IsYouTubeOpen = false;
            return;
        }

        IsSettingsOpen = false;
        IsRadioOpen = false;
        IsOnlineOpen = false;
        IsYouTubeOpen = true;

        if (YouTubeVideos.Count == 0 && !IsYouTubeLoading)
            RefreshYouTubeStatusLabel();
    }

    [RelayCommand]
    private async Task SearchYouTubeAsync() => await LoadYouTubeVideosAsync();

    /// <summary>
    /// Starts the selected video. Mimi's own transport is paused first so two
    /// audio surfaces never sound at once.
    /// </summary>
    [RelayCommand]
    private void PlayYouTubeVideo(YouTubeVideoItemViewModel? video)
    {
        if (video is null || disposed) return;

        SelectedYouTubeVideo = video;

        if (ReferenceEquals(video, CurrentYouTubeVideo))
        {
            // Clicking the row that is already playing closes the player,
            // which is also how YouTube playback is stopped.
            StopYouTubeVideo();
            return;
        }

        ReleaseOtherPlaybackSurfaces();
        CurrentYouTubeVideo = video;
        SelectedYouTubeVideo = video;
        NowPlayingTitle = video.Title;
        NowPlayingSubtitle = BuildYouTubeNowPlayingSubtitle(video);
        SetProgressPercent(0);
        CurrentTimeLabel = "0:00";
        DurationLabel = Loc["YouTube_ExternalTimeline"];
    }

    [RelayCommand]
    private void StopYouTubeVideo()
    {
        if (CurrentYouTubeVideo is null) return;

        CurrentYouTubeVideo = null;
        NowPlayingTitle = Loc["NowPlaying_ChooseSong"];
        NowPlayingSubtitle = Loc["NowPlaying_NextFavoriteWaiting"];
        SetProgressPercent(0);
        CurrentTimeLabel = "0:00";
        DurationLabel = "0:00";
        RefreshPlaybackState();
        RefreshYouTubePlaybackStates();
    }

    [RelayCommand]
    private void OpenCurrentYouTubeVideo()
    {
        var target = CurrentYouTubeVideo ?? SelectedYouTubeVideo;
        if (target is null) return;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(
                    target.Video.WatchUrl.AbsoluteUri)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not open a YouTube watch page: {ExceptionType}",
                ex.GetType().FullName);
            dialogService.ShowWarning(
                Loc["YouTube_OpenFailed"],
                Loc["YouTube_Title"]);
        }
    }

    private async Task LoadYouTubeVideosAsync()
    {
        if (disposed) return;

        if (youtubeSearchService is null)
        {
            SetYouTubeStatus(YouTubeSearchStatus.Unavailable, 0);
            return;
        }

        if (!YouTubeSearchEnabled)
        {
            SetYouTubeStatus(YouTubeSearchStatus.Unavailable, 0);
            return;
        }

        if (!youtubeSearchService.IsConfigured)
        {
            SetYouTubeStatus(YouTubeSearchStatus.NotConfigured, 0);
            return;
        }

        var query = YouTubeSearchText.Trim();
        if (query.Length == 0)
        {
            YouTubeVideos.Clear();
            SetYouTubeStatus(YouTubeSearchStatus.NotLoaded, 0);
            return;
        }

        youtubeSearchCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        youtubeSearchCancellation = cancellation;
        var version = ++youtubeSearchVersion;

        IsYouTubeLoading = true;
        SetYouTubeStatus(YouTubeSearchStatus.Loading, YouTubeVideos.Count);

        try
        {
            var videos = await youtubeSearchService.SearchAsync(
                query,
                cancellation.Token);

            if (disposed || version != youtubeSearchVersion ||
                cancellation.IsCancellationRequested)
            {
                return;
            }

            var selectedId = SelectedYouTubeVideo?.VideoId;
            YouTubeVideos.Clear();
            foreach (var video in videos)
                YouTubeVideos.Add(new YouTubeVideoItemViewModel(video));

            SelectedYouTubeVideo = YouTubeVideos.FirstOrDefault(item =>
                string.Equals(item.VideoId, selectedId, StringComparison.Ordinal)) ??
                YouTubeVideos.FirstOrDefault();

            SetYouTubeStatus(
                YouTubeVideos.Count == 0
                    ? YouTubeSearchStatus.Empty
                    : YouTubeSearchStatus.Ready,
                YouTubeVideos.Count);
            RefreshYouTubePlaybackStates();
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested ||
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (YouTubeInvalidKeyException)
        {
            if (disposed || version != youtubeSearchVersion) return;

            // The user can fix this from Settings, so say so instead of
            // reporting a generic network failure.
            SetYouTubeStatus(YouTubeSearchStatus.InvalidKey, YouTubeVideos.Count);
        }
        catch (YouTubeQuotaExceededException)
        {
            if (disposed || version != youtubeSearchVersion) return;

            // Expected on the free quota: 100 searches per day.
            SetYouTubeStatus(YouTubeSearchStatus.QuotaExceeded, YouTubeVideos.Count);
        }
        catch (Exception ex)
        {
            if (disposed || version != youtubeSearchVersion) return;

            // Never log the query or the API key.
            Log.Warning(
                "Could not search YouTube: {ExceptionType} (HRESULT 0x{HResult:X8})",
                ex.GetType().FullName,
                ex.HResult);
            SetYouTubeStatus(YouTubeSearchStatus.Failed, YouTubeVideos.Count);
        }
        finally
        {
            if (version == youtubeSearchVersion)
                IsYouTubeLoading = false;

            if (ReferenceEquals(youtubeSearchCancellation, cancellation))
                youtubeSearchCancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Claims the audible surface for YouTube: local playback is paused and the
    /// online/radio sources are released, so the embedded player is the only
    /// thing making sound.
    /// </summary>
    private void ReleaseOtherPlaybackSurfaces()
    {
        if (CurrentRadioStation is not null || IsRadioConnecting)
            DisableInternetRadio();

        if (CurrentOnlineTrack is not null)
        {
            hasOnlineSession = false;
            CurrentOnlineTrack = null;
            RefreshOnlineTrackPlaybackStates();
        }

        try
        {
            if (audioPlayer.IsPlaying)
                audioPlayer.Pause();
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not pause local playback for YouTube: {ExceptionType}",
                ex.GetType().FullName);
        }

        CurrentTrack = null;
        currentPlayHistoryId = null;
        RefreshPlaybackState();
    }

    private void RefreshYouTubePlaybackStates()
    {
        foreach (var video in YouTubeVideos)
            video.SetPlaybackState(ReferenceEquals(video, CurrentYouTubeVideo));
    }

    private void SetYouTubeStatus(YouTubeSearchStatus status, int resultCount)
    {
        youtubeSearchStatus = status;
        youtubeSearchResultCount = Math.Max(0, resultCount);
        RefreshYouTubeStatusLabel();
    }

    private void RefreshYouTubeStatusLabel()
    {
        YouTubeStatusLabel = youtubeSearchStatus switch
        {
            YouTubeSearchStatus.Loading => Loc["YouTube_Loading"],
            YouTubeSearchStatus.Ready => Loc.Format(
                "YouTube_ResultsAvailable",
                youtubeSearchResultCount),
            YouTubeSearchStatus.Empty => Loc["YouTube_NoResults"],
            YouTubeSearchStatus.Failed => Loc["YouTube_LoadFailed"],
            YouTubeSearchStatus.QuotaExceeded => Loc["YouTube_QuotaExceeded"],
            YouTubeSearchStatus.InvalidKey => Loc["YouTube_InvalidKey"],
            YouTubeSearchStatus.NotConfigured => Loc.Format(
                "YouTube_NotConfigured",
                YouTubeKeyFilePath),
            YouTubeSearchStatus.Unavailable => Loc["YouTube_Unavailable"],
            _ => Loc["YouTube_Description"]
        };
    }

    private string BuildYouTubeNowPlayingSubtitle(YouTubeVideoItemViewModel video) =>
        $"{video.ChannelTitle} · {Loc["YouTube_Source"]}";

    /// <summary>Settings kill switch: close the surface and stop the video.</summary>
    internal void DisableYouTube()
    {
        youtubeSearchCancellation?.Cancel();
        youtubeSearchVersion++;
        IsYouTubeOpen = false;
        IsYouTubeLoading = false;
        StopYouTubeVideo();
        SetYouTubeStatus(
            YouTubeVideos.Count == 0
                ? YouTubeSearchStatus.NotLoaded
                : YouTubeSearchStatus.Ready,
            YouTubeVideos.Count);
    }
}
