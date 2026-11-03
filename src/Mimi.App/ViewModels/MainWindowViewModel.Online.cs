using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Online;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// On-demand online catalog: search a permitted public provider inside Mimi
/// and stream the selected track through Mimi's own audio engine.
///
/// Online items stay a separate playback source. They never become library
/// tracks, never enter the queue, playback history, favorites, lyrics, or the
/// saved session, and Mimi never downloads, proxies, or persists their audio.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private enum OnlineCatalogStatus
    {
        NotLoaded,
        Loading,
        Ready,
        SearchEmpty,
        Failed,
        Unavailable
    }

    private readonly IOnlineCatalogService? onlineCatalogService;
    private CancellationTokenSource? onlineSearchCancellation;
    private OnlineTrackItemViewModel? connectingOnlineTrack;
    private OnlineCatalogStatus onlineCatalogStatus = OnlineCatalogStatus.NotLoaded;
    private int onlineCatalogResultCount;
    private int onlineSearchVersion;

    // True while the last playback source the user chose was the online
    // catalog. It keeps an ended online track from silently handing the
    // engine back to a local queue item.
    private bool hasOnlineSession;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsOnlineContentVisible))]
    private bool isOnlineOpen;

    [ObservableProperty]
    private string onlineSearchText = string.Empty;

    [ObservableProperty]
    private bool isOnlineLoading;

    [ObservableProperty]
    private bool isOnlineConnecting;

    [ObservableProperty]
    private string onlineStatusLabel = string.Empty;

    [ObservableProperty]
    private OnlineTrackItemViewModel? selectedOnlineTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentPlaybackSource))]
    [NotifyPropertyChangedFor(nameof(TaskbarDescription))]
    [NotifyPropertyChangedFor(nameof(TaskbarProgressState))]
    private OnlineTrackItemViewModel? currentOnlineTrack;

    public ObservableCollection<OnlineTrackItemViewModel> OnlineTracks { get; } = [];

    public bool IsOnlineCatalogInstalled => onlineCatalogService is not null;

    /// <summary>
    /// Local user switch. It hides the online surface and is not a provider
    /// grant or a production approval.
    /// </summary>
    public bool IsOnlineAvailable =>
        OnlineCatalogEnabled && onlineCatalogService is not null;

    public bool IsOnlineContentVisible => IsOnlineOpen && !IsSettingsOpen;

    partial void OnCurrentOnlineTrackChanged(OnlineTrackItemViewModel? value) =>
        RefreshOnlineTrackPlaybackStates();

    partial void OnIsOnlineConnectingChanged(bool value) =>
        RefreshOnlineTrackPlaybackStates();

    [RelayCommand]
    private void ShowOnline()
    {
        if (!IsOnlineAvailable)
        {
            IsOnlineOpen = false;
            return;
        }

        IsSettingsOpen = false;
        IsRadioOpen = false;
        IsOnlineOpen = true;

        if (OnlineTracks.Count == 0 && !IsOnlineLoading)
            RefreshOnlineStatusLabel();
    }

    [RelayCommand]
    private async Task SearchOnlineAsync() => await LoadOnlineTracksAsync();

    [RelayCommand]
    private async Task PlayOnlineTrackAsync(OnlineTrackItemViewModel? track)
    {
        if (track is null || disposed) return;

        SelectedOnlineTrack = track;
        if (ReferenceEquals(track, CurrentOnlineTrack))
        {
            if (audioPlayer.IsPlaying)
            {
                audioPlayer.Pause();
                RefreshPlaybackState();
                RefreshOnlineTrackPlaybackStates();
                return;
            }

            // Online tracks are finite, so resuming continues where it
            // stopped instead of reconnecting like a live station.
            try
            {
                ApplyEffectiveVolume();
                audioPlayer.Play();
                RefreshPlaybackState();
            }
            catch (AudioOutputUnavailableException ex)
            {
                ShowAudioOutputUnavailable(ex);
            }

            RefreshOnlineTrackPlaybackStates();
            return;
        }

        await StartOnlineTrackAsync(track);
    }

    /// <summary>
    /// True when <see cref="PlayPause"/> must handle the press itself, so a
    /// finished or paused online track never restarts a local song.
    /// </summary>
    private bool TryToggleOnlinePlayback()
    {
        if (!hasOnlineSession && CurrentOnlineTrack is null) return false;

        if (CurrentOnlineTrack is null)
        {
            // Nothing adopted yet (for example the last result finished):
            // replay the selection rather than borrowing a library track.
            var target = SelectedOnlineTrack ?? OnlineTracks.FirstOrDefault();
            if (target is null)
            {
                ShowOnlineSessionEnded();
                return true;
            }

            _ = StartOnlineTrackAsync(target);
            return true;
        }

        try
        {
            if (audioPlayer.IsPlaying)
                audioPlayer.Pause();
            else
            {
                ApplyEffectiveVolume();
                audioPlayer.Play();
            }
        }
        catch (AudioOutputUnavailableException ex)
        {
            ShowAudioOutputUnavailable(ex);
        }

        RefreshPlaybackState();
        RefreshOnlineTrackPlaybackStates();
        return true;
    }

    [RelayCommand]
    private void OpenOnlineTrackPage(OnlineTrackItemViewModel? track)
    {
        var permalink = track?.Permalink;
        if (permalink is null) return;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(permalink.AbsoluteUri)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not open the canonical page for an online track: {ExceptionType}",
                ex.GetType().FullName);
            dialogService.ShowWarning(
                Loc["Online_OpenSourceFailed"],
                Loc["Online_Title"]);
        }
    }

    private async Task LoadOnlineTracksAsync()
    {
        if (disposed) return;

        if (!OnlineCatalogEnabled || onlineCatalogService is null)
        {
            SetOnlineCatalogStatus(OnlineCatalogStatus.Unavailable, 0);
            return;
        }

        var query = OnlineSearchText.Trim();
        if (query.Length == 0)
        {
            OnlineTracks.Clear();
            SetOnlineCatalogStatus(OnlineCatalogStatus.NotLoaded, 0);
            return;
        }

        onlineSearchCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        onlineSearchCancellation = cancellation;
        var version = ++onlineSearchVersion;

        IsOnlineLoading = true;
        SetOnlineCatalogStatus(OnlineCatalogStatus.Loading, OnlineTracks.Count);

        try
        {
            var tracks = await onlineCatalogService.SearchTracksAsync(
                query,
                cancellation.Token);

            if (disposed || version != onlineSearchVersion ||
                cancellation.IsCancellationRequested)
            {
                return;
            }

            var selectedId = SelectedOnlineTrack?.Id;
            OnlineTracks.Clear();
            foreach (var track in tracks)
                OnlineTracks.Add(new OnlineTrackItemViewModel(track));

            SelectedOnlineTrack = OnlineTracks.FirstOrDefault(item =>
                string.Equals(item.Id, selectedId, StringComparison.Ordinal)) ??
                OnlineTracks.FirstOrDefault();

            SetOnlineCatalogStatus(
                OnlineTracks.Count == 0
                    ? OnlineCatalogStatus.SearchEmpty
                    : OnlineCatalogStatus.Ready,
                OnlineTracks.Count);
            RefreshOnlineTrackPlaybackStates();
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested ||
            lifetimeCancellation.IsCancellationRequested)
        {
            // A newer search owns the status label and the result list.
        }
        catch (Exception ex)
        {
            if (disposed || version != onlineSearchVersion) return;

            // Never log the user's query or a provider stream URL.
            Log.Warning(
                "Could not search the online catalog: {ExceptionType} (HRESULT 0x{HResult:X8})",
                ex.GetType().FullName,
                ex.HResult);
            SetOnlineCatalogStatus(OnlineCatalogStatus.Failed, OnlineTracks.Count);
        }
        finally
        {
            if (version == onlineSearchVersion)
                IsOnlineLoading = false;

            if (ReferenceEquals(onlineSearchCancellation, cancellation))
                onlineSearchCancellation = null;
            cancellation.Dispose();
        }
    }

    private async Task StartOnlineTrackAsync(OnlineTrackItemViewModel track)
    {
        if (disposed || IsOnlineConnecting) return;

        if (onlineCatalogService is null || !OnlineCatalogEnabled)
        {
            SetOnlineCatalogStatus(OnlineCatalogStatus.Unavailable, OnlineTracks.Count);
            dialogService.ShowWarning(Loc["Online_Unavailable"], Loc["Online_Title"]);
            return;
        }

        // Taking over playback also abandons any pending radio station, using
        // the same version/lock protocol as a local file click.
        var version = BeginLocalPlaybackAttempt();
        connectingOnlineTrack = track;
        IsOnlineConnecting = true;
        SelectedOnlineTrack = track;
        RefreshOnlineTrackPlaybackStates();

        var loaded = false;
        try
        {
            var streamUrl = track.StreamUrl;

            loaded = await Task.Run(() =>
            {
                lock (playbackSourceLoadSync)
                {
                    if (Volatile.Read(ref playbackSourceVersion) != version)
                        return false;

                    audioPlayer.LoadRemoteAudio(streamUrl);
                    return true;
                }
            });

            if (!loaded || !IsCurrentPlaybackVersion(version)) return;

            hasOnlineSession = true;
            CurrentOnlineTrack = track;
            CurrentRadioStation = null;
            CurrentTrack = null;
            currentPlayHistoryId = null;
            CurrentCoverArtPath = null;
            playbackQueue.MoveCurrentToHistory();
            RefreshUpcomingQueue();

            ApplyEffectiveVolume();
            NowPlayingTitle = track.Title;
            NowPlayingSubtitle = BuildOnlineNowPlayingSubtitle(track);
            SetProgressPercent(0);
            CurrentTimeLabel = "0:00";
            DurationLabel = track.DurationLabel;

            audioPlayer.Play();
            RefreshPlaybackState();
            RefreshOnlineTrackPlaybackStates();
        }
        catch (AudioOutputUnavailableException ex)
        {
            if (!IsCurrentPlaybackVersion(version)) return;

            if (!loaded)
                ClearPlaybackAfterFailedOnlineLoad();

            ShowAudioOutputUnavailable(ex);
            RefreshOnlineTrackPlaybackStates();
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsCurrentPlaybackVersion(version)) return;

            if (loaded)
            {
                try
                {
                    audioPlayer.Stop();
                }
                catch (Exception stopException)
                {
                    Log.Debug(
                        "Could not stop a failed online track: {ExceptionType}",
                        stopException.GetType().FullName);
                }
            }

            ClearPlaybackAfterFailedOnlineLoad();
            RefreshPlaybackState();
            Log.Warning(
                "Could not play an online catalog track: {ExceptionType} (HRESULT 0x{HResult:X8})",
                ex.GetType().FullName,
                ex.HResult);
            dialogService.ShowWarning(Loc["Online_PlayFailed"], Loc["Online_Title"]);
        }
        finally
        {
            if (version == Volatile.Read(ref playbackSourceVersion))
            {
                connectingOnlineTrack = null;
                IsOnlineConnecting = false;
                RefreshOnlineTrackPlaybackStates();
            }
        }
    }

    /// <summary>
    /// Natural end of a finite online track: continue with the next visible
    /// result instead of silently stopping in the middle of a list.
    /// </summary>
    private bool TryAdvanceOnlineTrack()
    {
        if (!hasOnlineSession) return false;
        if (CurrentOnlineTrack is null) return false;

        var currentIndex = OnlineTracks.IndexOf(CurrentOnlineTrack);
        var nextIndex = currentIndex < 0 ? 0 : currentIndex + 1;
        if (nextIndex >= OnlineTracks.Count) return false;

        var next = OnlineTracks[nextIndex];
        SelectedOnlineTrack = next;
        _ = StartOnlineTrackAsync(next);
        return true;
    }

    /// <summary>
    /// The last result finished. Stay on the online surface and say so
    /// instead of falling back to whatever local song happens to be selected.
    /// </summary>
    private void ShowOnlineSessionEnded()
    {
        SetProgressPercent(0);
        CurrentTimeLabel = "0:00";
        DurationLabel = "0:00";
        RefreshPlaybackState();
        RefreshOnlineTrackPlaybackStates();
    }

    private bool TryHandleOnlineTrackNavigation(int offset)
    {
        var onlineOwnsPlayback = hasOnlineSession || CurrentOnlineTrack is not null;
        if (!onlineOwnsPlayback) return false;
        if (OnlineTracks.Count == 0 || IsOnlineConnecting) return true;

        // After the last result finishes there is no current track, so the
        // list continues from the selected row instead of jumping away.
        var anchor = CurrentOnlineTrack ?? SelectedOnlineTrack;
        var currentIndex = anchor is null ? -1 : OnlineTracks.IndexOf(anchor);
        var targetIndex = currentIndex < 0
            ? (offset < 0 ? OnlineTracks.Count - 1 : 0)
            : currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= OnlineTracks.Count) return true;

        var target = OnlineTracks[targetIndex];
        if (ReferenceEquals(target, CurrentOnlineTrack)) return true;

        SelectedOnlineTrack = target;
        _ = StartOnlineTrackAsync(target);
        return true;
    }

    private void ClearPlaybackAfterFailedOnlineLoad()
    {
        CurrentOnlineTrack = null;
        CurrentRadioStation = null;
        CurrentTrack = null;
        currentPlayHistoryId = null;
        CurrentCoverArtPath = null;
        playbackQueue.MoveCurrentToHistory();
        RefreshUpcomingQueue();
        NowPlayingTitle = Loc["NowPlaying_ChooseSong"];
        NowPlayingSubtitle = Loc["NowPlaying_NextFavoriteWaiting"];
        SetProgressPercent(0);
        CurrentTimeLabel = "0:00";
        DurationLabel = "0:00";
        RefreshOnlineTrackPlaybackStates();
    }

    private void RefreshOnlineTrackPlaybackStates()
    {
        foreach (var track in OnlineTracks)
        {
            track.SetPlaybackState(
                current: ReferenceEquals(track, CurrentOnlineTrack),
                playing: IsPlaying,
                connecting: ReferenceEquals(track, connectingOnlineTrack));
        }
    }

    private void SetOnlineCatalogStatus(
        OnlineCatalogStatus status,
        int resultCount)
    {
        onlineCatalogStatus = status;
        onlineCatalogResultCount = Math.Max(0, resultCount);
        RefreshOnlineStatusLabel();
    }

    private void RefreshOnlineStatusLabel()
    {
        OnlineStatusLabel = onlineCatalogStatus switch
        {
            OnlineCatalogStatus.Loading => Loc["Online_Loading"],
            OnlineCatalogStatus.Ready => Loc.Format(
                "Online_ResultsAvailable",
                onlineCatalogResultCount),
            OnlineCatalogStatus.SearchEmpty => Loc["Online_NoResults"],
            OnlineCatalogStatus.Failed => Loc["Online_LoadFailed"],
            OnlineCatalogStatus.Unavailable => Loc["Online_Unavailable"],
            _ => Loc["Online_Description"]
        };
    }

    private string BuildOnlineNowPlayingSubtitle(OnlineTrackItemViewModel track) =>
        track.Artist.Length == 0
            ? Loc["Online_Source"]
            : $"{track.Artist} · {Loc["Online_Source"]}";

    /// <summary>
    /// Settings kill switch: cancel work, close the surface, and stop only an
    /// online track. Local playback and the Radio catalog are untouched.
    /// </summary>
    internal void DisableOnlineCatalog()
    {
        onlineSearchCancellation?.Cancel();
        onlineSearchVersion++;
        IsOnlineOpen = false;
        IsOnlineLoading = false;
        hasOnlineSession = false;
        connectingOnlineTrack = null;
        IsOnlineConnecting = false;
        SetOnlineCatalogStatus(
            OnlineTracks.Count == 0
                ? OnlineCatalogStatus.NotLoaded
                : OnlineCatalogStatus.Ready,
            OnlineTracks.Count);

        if (CurrentOnlineTrack is null)
        {
            RefreshOnlineTrackPlaybackStates();
            return;
        }

        try
        {
            audioPlayer.Stop();
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not stop the online track for the kill switch: {ExceptionType}",
                ex.GetType().FullName);
        }

        ClearPlaybackAfterFailedOnlineLoad();
        RefreshPlaybackState();
    }
}
