using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Core.Playback;
using Mimi.YouTube;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>What Mimi's transport asks the embedded player to do.</summary>
public enum EmbeddedPlayerCommandKind
{
    Play,
    Pause,
    Seek,
    SetVolume,
    Restart
}

/// <summary>
/// One transport request for the embedded player. Seeking carries seconds
/// because the page owns the timeline, not Mimi.
/// </summary>
public readonly record struct EmbeddedPlayerCommand(
    EmbeddedPlayerCommandKind Kind,
    double Seconds = 0);

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

    // Official IFrame API player states.
    private const int YouTubeStateEnded = 0;
    private const int YouTubeStatePlaying = 1;
    private const int YouTubeStateBuffering = 3;

    private readonly IYouTubeSearchService? youtubeSearchService;
    private CancellationTokenSource? youtubeSearchCancellation;
    private YouTubeSearchStatus youtubeSearchStatus = YouTubeSearchStatus.NotLoaded;
    private int youtubeSearchResultCount;
    private int youtubeSearchVersion;
    private double youTubePositionSeconds;
    private double youTubeDurationSeconds;
    private bool isYouTubeVideoPlaying;
    private bool youTubeEndHandled;

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

    /// <summary>
    /// Volume the page is opened with. The embedded player keeps its own
    /// volume, so it has to be handed over at start and again on every change.
    /// </summary>
    public int YouTubeVolumePercent =>
        (int)Math.Round(Math.Clamp(IsMuted ? 0 : Volume, 0, 1) * 100);

    public string YouTubeKeyFilePath =>
        youtubeSearchService?.KeyFilePath ?? string.Empty;

    /// <summary>
    /// Raised when the transport (Play/Pause, the slider, -10s/+10s) needs the
    /// embedded player to act. The window owns the WebView2, so the view model
    /// only asks and never touches the browser.
    /// </summary>
    public event Action<EmbeddedPlayerCommand>? EmbeddedPlayerCommandRequested;

    /// <summary>
    /// Asks the window to print what the player page actually reports. The
    /// transport has failed silently once already, so there is a way to get a
    /// straight answer out of the page instead of another round of guessing.
    /// </summary>
    public event Action? EmbeddedPlayerDiagnosticRequested;

    [RelayCommand]
    private void DiagnoseYouTubePlayer()
    {
        if (!IsYouTubePlaying) return;

        EmbeddedPlayerDiagnosticRequested?.Invoke();
    }

    /// <summary>Shows the raw bridge answer in the panel's status line.</summary>
    public void ApplyEmbeddedPlayerDiagnostic(string report)
    {
        if (disposed) return;

        YouTubeStatusLabel = Loc.Format("YouTube_DiagnosticFormat", report);
    }

    /// <summary>
    /// Applies a position report from the official player. Everything the user
    /// sees about YouTube progress comes from here, because the audio never
    /// passes through Mimi's own pipeline.
    /// </summary>
    public void ApplyEmbeddedPlayerState(
        int state,
        double positionSeconds,
        double durationSeconds)
    {
        if (disposed || !IsYouTubePlaying) return;

        // The official player reports zero when the video has finished. It can
        // repeat that report on the next poll, so act once per ending.
        if (state == YouTubeStateEnded)
        {
            if (youTubeEndHandled) return;

            youTubeEndHandled = true;
            HandleYouTubeVideoEnded();
            return;
        }

        youTubeEndHandled = false;

        if (durationSeconds > 0.5)
            youTubeDurationSeconds = durationSeconds;

        if (positionSeconds >= 0)
            youTubePositionSeconds = positionSeconds;

        isYouTubeVideoPlaying =
            state is YouTubeStatePlaying or YouTubeStateBuffering;

        if (!userSeeking)
        {
            if (youTubeDurationSeconds > 0)
            {
                SetProgressPercent(Math.Clamp(
                    youTubePositionSeconds / youTubeDurationSeconds * 100d,
                    0,
                    100));
            }

            CurrentTimeLabel = FormatTime(
                TimeSpan.FromSeconds(youTubePositionSeconds));
            DurationLabel = FormatTime(
                TimeSpan.FromSeconds(youTubeDurationSeconds));
        }

        RefreshPlaybackState();
    }

    /// <summary>
    /// Handles the player's own error codes. 101 and 150 are the official
    /// "the owner disabled embedding" codes, which no amount of retrying fixes,
    /// so the honest answer is to say so and point at the Open button.
    /// </summary>
    public void ReportEmbeddedPlayerError(int code)
    {
        if (disposed) return;

        if (code is not (101 or 150))
        {
            Log.Debug("The YouTube player reported error code {Code}", code);
            return;
        }

        StopYouTubeVideo();
        YouTubeStatusLabel = Loc["YouTube_EmbeddingBlocked"];
    }

    /// <summary>
    /// The page never produced a player, so the transport cannot describe it.
    /// Saying so is better than leaving a progress bar that never moves.
    /// </summary>
    public void ReportEmbeddedPlayerUnavailable()
    {
        if (disposed || !IsYouTubePlaying) return;

        YouTubeStatusLabel = Loc["YouTube_BridgeUnavailable"];
    }

    /// <summary>
    /// The search results are the play order for YouTube, so the transport's
    /// next/previous and the repeat mode work without a second queue that would
    /// have to be kept in step with the list the user is looking at.
    /// </summary>
    private bool TryHandleYouTubeNavigation(int offset)
    {
        if (!IsYouTubePlaying) return false;

        PlayAdjacentYouTubeVideo(offset, allowWrap: true);
        return true;
    }

    private bool PlayAdjacentYouTubeVideo(int offset, bool allowWrap)
    {
        if (!IsYouTubePlaying || YouTubeVideos.Count == 0) return false;

        var current = CurrentYouTubeVideo;
        if (current is null) return false;

        var index = YouTubeVideos.IndexOf(current);
        if (index < 0) return false;

        int target;
        if (offset > 0 && IsShuffleEnabled && YouTubeVideos.Count > 1)
        {
            do
            {
                target = Random.Shared.Next(YouTubeVideos.Count);
            }
            while (target == index);
        }
        else
        {
            target = index + offset;

            if (target < 0 || target >= YouTubeVideos.Count)
            {
                if (!allowWrap) return false;

                target = (target + YouTubeVideos.Count) % YouTubeVideos.Count;
            }
        }

        StartYouTubeVideo(YouTubeVideos[target]);
        return true;
    }

    /// <summary>End of a video follows the same repeat rules as a local track.</summary>
    private void HandleYouTubeVideoEnded()
    {
        if (RepeatMode == RepeatMode.One)
        {
            youTubeEndHandled = false;
            RequestEmbeddedPlayer(EmbeddedPlayerCommandKind.Restart);
            return;
        }

        if (PlayAdjacentYouTubeVideo(1, allowWrap: RepeatMode == RepeatMode.All))
            return;

        StopYouTubeVideo();
    }

    /// <summary>Starts the list from the selection: the "play all" of this panel.</summary>
    [RelayCommand]
    private void PlayAllYouTubeVideos()
    {
        if (disposed || YouTubeVideos.Count == 0) return;

        var first = SelectedYouTubeVideo ?? YouTubeVideos[0];
        StartYouTubeVideo(first);
    }

    private void PushYouTubeVolume()
    {
        if (!IsYouTubePlaying) return;

        // Muting is expressed as volume 0; the page unmutes itself when the
        // value climbs back above zero, so one command covers both controls.
        RequestEmbeddedPlayer(
            EmbeddedPlayerCommandKind.SetVolume,
            YouTubeVolumePercent);
    }

    private void RequestEmbeddedPlayer(
        EmbeddedPlayerCommandKind kind,
        double seconds = 0)
    {
        if (CurrentYouTubeVideo is null) return;

        EmbeddedPlayerCommandRequested?.Invoke(
            new EmbeddedPlayerCommand(kind, seconds));
    }

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

        StartYouTubeVideo(video);
    }

    /// <summary>
    /// Starts one result without the toggle behaviour of the row click, so the
    /// transport can move through the list without closing the player.
    /// </summary>
    private void StartYouTubeVideo(YouTubeVideoItemViewModel video)
    {
        if (disposed) return;

        SelectedYouTubeVideo = video;

        if (ReferenceEquals(video, CurrentYouTubeVideo))
        {
            // Already on screen: restart it instead of reloading the page.
            youTubePositionSeconds = 0;
            youTubeEndHandled = false;
            SetProgressPercent(0);
            CurrentTimeLabel = "0:00";
            RequestEmbeddedPlayer(EmbeddedPlayerCommandKind.Restart);
            return;
        }

        ReleaseOtherPlaybackSurfaces();

        youTubePositionSeconds = 0;
        youTubeDurationSeconds = 0;
        isYouTubeVideoPlaying = false;
        youTubeEndHandled = false;

        CurrentYouTubeVideo = video;
        SelectedYouTubeVideo = video;
        NowPlayingTitle = video.Title;
        NowPlayingSubtitle = BuildYouTubeNowPlayingSubtitle(video);
        SetProgressPercent(0);
        CurrentTimeLabel = "0:00";
        // The real duration arrives from the player a moment later.
        DurationLabel = "0:00";
        RefreshPlaybackState();
    }

    [RelayCommand]
    private void StopYouTubeVideo()
    {
        if (CurrentYouTubeVideo is null) return;

        CurrentYouTubeVideo = null;
        youTubePositionSeconds = 0;
        youTubeDurationSeconds = 0;
        isYouTubeVideoPlaying = false;
        youTubeEndHandled = false;
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
