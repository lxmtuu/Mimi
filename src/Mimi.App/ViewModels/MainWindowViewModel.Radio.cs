using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Opt-in Internet Radio presentation and playback. Radio stations remain a
/// separate playback source from local tracks: they are not added to the
/// library, queue, playback history, favorites, lyrics, or saved session.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private enum RadioCatalogStatus
    {
        NotLoaded,
        Loading,
        Ready,
        Empty,
        SearchEmpty,
        Failed,
        Unavailable
    }

    private readonly IRadioStationService? radioStationService;
    private CancellationTokenSource? radioSearchCancellation;
    private RadioStationItemViewModel? connectingRadioStation;
    private RadioCatalogStatus radioCatalogStatus = RadioCatalogStatus.NotLoaded;
    private int radioCatalogStationCount;
    private bool radioCatalogWasSearch;
    private bool radioCatalogLoaded;
    private int radioSearchVersion;
    private int playbackSourceVersion;
    private readonly object playbackSourceLoadSync = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsRadioContentVisible))]
    private bool isRadioOpen;

    [ObservableProperty]
    private string radioSearchText = string.Empty;

    [ObservableProperty]
    private bool isRadioLoading;

    [ObservableProperty]
    private bool isRadioConnecting;

    [ObservableProperty]
    private string radioStatusLabel = string.Empty;

    [ObservableProperty]
    private RadioStationItemViewModel? selectedRadioStation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentPlaybackSource))]
    [NotifyPropertyChangedFor(nameof(IsLivePlayback))]
    [NotifyPropertyChangedFor(nameof(TaskbarDescription))]
    [NotifyPropertyChangedFor(nameof(TaskbarProgressState))]
    private RadioStationItemViewModel? currentRadioStation;

    public ObservableCollection<RadioStationItemViewModel> RadioStations { get; } = [];

    public bool IsRadioServiceInstalled => radioStationService is not null;

    public bool IsRadioAvailable =>
        InternetRadioEnabled && radioStationService is not null;

    public bool IsRadioContentVisible => IsRadioOpen && !IsSettingsOpen;

    public bool HasCurrentPlaybackSource =>
        CurrentTrack is not null ||
        CurrentRadioStation is not null ||
        CurrentOnlineTrack is not null;

    public bool IsLivePlayback => CurrentRadioStation is not null;

    partial void OnCurrentRadioStationChanged(RadioStationItemViewModel? value) =>
        RefreshRadioStationPlaybackStates();

    partial void OnIsRadioConnectingChanged(bool value) =>
        RefreshRadioStationPlaybackStates();

    [RelayCommand]
    private async Task ShowRadioAsync()
    {
        if (!IsRadioAvailable)
        {
            IsRadioOpen = false;
            return;
        }

        IsSettingsOpen = false;
        IsOnlineOpen = false;
        IsRadioOpen = true;

        if (!radioCatalogLoaded && !IsRadioLoading)
            await LoadRadioStationsAsync();
    }

    [RelayCommand]
    private async Task SearchRadioAsync() => await LoadRadioStationsAsync();

    [RelayCommand]
    private async Task RefreshRadioStationsAsync() => await LoadRadioStationsAsync();

    [RelayCommand]
    private async Task PlayRadioStationAsync(RadioStationItemViewModel? station)
    {
        if (station is null || disposed) return;

        SelectedRadioStation = station;
        if (ReferenceEquals(station, CurrentRadioStation))
        {
            if (audioPlayer.IsPlaying)
            {
                audioPlayer.Pause();
                RefreshPlaybackState();
                RefreshRadioStationPlaybackStates();
                return;
            }

            // Resuming a live station reconnects to the live edge instead of
            // trying to seek or replay buffered network audio.
            await StartRadioStationAsync(station, registerClick: false);
            return;
        }

        await StartRadioStationAsync(station, registerClick: true);
    }

    [RelayCommand]
    private void OpenRadioHomepage(RadioStationItemViewModel? station)
    {
        var homepage = station?.HomepageUrl;
        if (homepage is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(homepage.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Warning(
                "Could not open the homepage for radio station {StationUuid}: {ExceptionType}",
                station!.StationUuid,
                ex.GetType().FullName);
            dialogService.ShowWarning(
                Loc["Radio_HomepageOpenFailed"],
                Loc["Radio_Title"]);
        }
    }

    private async Task LoadRadioStationsAsync()
    {
        if (disposed) return;

        if (!InternetRadioEnabled)
        {
            SetRadioCatalogStatus(
                RadioCatalogStatus.Unavailable,
                RadioStations.Count,
                radioCatalogWasSearch);
            return;
        }

        if (radioStationService is null)
        {
            RadioStations.Clear();
            SetRadioCatalogStatus(
                RadioCatalogStatus.Unavailable,
                stationCount: 0,
                wasSearch: false);
            return;
        }

        var query = RadioSearchText.Trim();
        var isSearch = query.Length > 0;

        radioSearchCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        radioSearchCancellation = cancellation;
        var version = ++radioSearchVersion;

        IsRadioLoading = true;
        SetRadioCatalogStatus(
            RadioCatalogStatus.Loading,
            RadioStations.Count,
            isSearch);

        try
        {
            var stations = isSearch
                ? await radioStationService.SearchStationsAsync(
                    query,
                    cancellation.Token)
                : await radioStationService.GetTopStationsAsync(cancellation.Token);

            if (disposed || version != radioSearchVersion ||
                cancellation.IsCancellationRequested)
            {
                return;
            }

            var selectedStationUuid = SelectedRadioStation?.StationUuid;
            var existingByUuid = RadioStations
                .GroupBy(station => station.StationUuid)
                .ToDictionary(group => group.Key, group => group.First());

            RadioStations.Clear();
            foreach (var station in stations)
            {
                RadioStations.Add(
                    existingByUuid.TryGetValue(station.StationUuid, out var existing)
                        ? existing
                        : new RadioStationItemViewModel(station));
            }

            SelectedRadioStation =
                RadioStations.FirstOrDefault(station =>
                    station.StationUuid == selectedStationUuid) ??
                RadioStations.FirstOrDefault();
            radioCatalogLoaded = true;

            SetRadioCatalogStatus(
                RadioStations.Count == 0
                    ? isSearch
                        ? RadioCatalogStatus.SearchEmpty
                        : RadioCatalogStatus.Empty
                    : RadioCatalogStatus.Ready,
                RadioStations.Count,
                isSearch);
            RefreshRadioStationPlaybackStates();
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested ||
            lifetimeCancellation.IsCancellationRequested)
        {
            // A newer search owns the status label and result list.
        }
        catch (Exception ex)
        {
            if (disposed || version != radioSearchVersion) return;

            // Do not log the user's search text or any provider URL.
            Log.Warning(
                "Could not load Radio Browser stations: {ExceptionType} (HRESULT 0x{HResult:X8})",
                ex.GetType().FullName,
                ex.HResult);
            SetRadioCatalogStatus(
                RadioCatalogStatus.Failed,
                RadioStations.Count,
                isSearch);
        }
        finally
        {
            if (version == radioSearchVersion)
                IsRadioLoading = false;

            if (ReferenceEquals(radioSearchCancellation, cancellation))
                radioSearchCancellation = null;
            cancellation.Dispose();
        }
    }

    private async Task StartRadioStationAsync(
        RadioStationItemViewModel station,
        bool registerClick)
    {
        if (disposed || IsRadioConnecting) return;

        if (radioStationService is null || !InternetRadioEnabled)
        {
            SetRadioCatalogStatus(
                RadioCatalogStatus.Unavailable,
                RadioStations.Count,
                radioCatalogWasSearch);
            dialogService.ShowWarning(
                Loc["Radio_Unavailable"],
                Loc["Radio_Title"]);
            return;
        }

        var version = Interlocked.Increment(ref playbackSourceVersion);
        connectingRadioStation = station;
        IsRadioConnecting = true;
        SelectedRadioStation = station;
        hasOnlineSession = false;
        connectingOnlineTrack = null;
        IsOnlineConnecting = false;
        CurrentOnlineTrack = null;
        RefreshOnlineTrackPlaybackStates();
        RefreshRadioStationPlaybackStates();

        var loaded = false;
        try
        {
            var streamUrl = station.StreamUrl;

            // Keep the version check and the side-effectful stream load
            // atomic with respect to BeginLocalPlaybackAttempt. Otherwise a
            // radio task scheduled before a local click could finish loading
            // after the local file and leave the audio engine pointing at a
            // station the UI has already discarded.
            loaded = await Task.Run(() =>
            {
                lock (playbackSourceLoadSync)
                {
                    if (Volatile.Read(ref playbackSourceVersion) != version)
                        return false;

                    audioPlayer.LoadStream(streamUrl);
                    return true;
                }
            });

            if (!loaded || !IsCurrentPlaybackVersion(version)) return;

            CurrentRadioStation = station;
            CurrentTrack = null;
            currentPlayHistoryId = null;
            CurrentCoverArtPath = null;
            playbackQueue.MoveCurrentToHistory();
            RefreshUpcomingQueue();

            ApplyEffectiveVolume();
            NowPlayingTitle = station.Name;
            NowPlayingSubtitle = BuildRadioNowPlayingSubtitle(station);
            SetProgressPercent(0);
            CurrentTimeLabel = "0:00";
            DurationLabel = Loc["Radio_Live"];

            audioPlayer.Play();
            RefreshPlaybackState();
            RefreshRadioStationPlaybackStates();

            if (registerClick)
                _ = RegisterRadioClickAsync(station);
        }
        catch (AudioOutputUnavailableException ex)
        {
            if (!IsCurrentPlaybackVersion(version)) return;

            if (!loaded)
                ClearPlaybackAfterFailedRadioLoad();

            // When the stream loaded, the station stays adopted and paused;
            // a missing output is not a dead-station error and does not earn
            // a provider click because playback has not actually started.
            ShowAudioOutputUnavailable(ex);
            RefreshRadioStationPlaybackStates();
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
                        "Could not stop a failed radio stream for {StationUuid}: {ExceptionType}",
                        station.StationUuid,
                        stopException.GetType().FullName);
                }
            }

            ClearPlaybackAfterFailedRadioLoad();
            RefreshPlaybackState();
            Log.Warning(
                "Could not play radio station {StationUuid}: {ExceptionType} (HRESULT 0x{HResult:X8})",
                station.StationUuid,
                ex.GetType().FullName,
                ex.HResult);
            dialogService.ShowWarning(
                Loc["Radio_PlayFailed"],
                Loc["Radio_Title"]);
        }
        finally
        {
            if (version == Volatile.Read(ref playbackSourceVersion))
            {
                connectingRadioStation = null;
                IsRadioConnecting = false;
                RefreshRadioStationPlaybackStates();
            }
        }
    }

    private async Task RegisterRadioClickAsync(RadioStationItemViewModel station)
    {
        if (radioStationService is null) return;

        try
        {
            await radioStationService.RegisterStationClickAsync(
                station.StationUuid,
                lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (
            lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Attribution/statistics are best effort and must never disturb a
            // station that is already playing.
            Log.Debug(
                "Could not register a Radio Browser station click for {StationUuid}: {ExceptionType}",
                station.StationUuid,
                ex.GetType().FullName);
        }
    }

    private bool TryHandleRadioStationNavigation(int offset)
    {
        if (CurrentRadioStation is null) return false;
        if (RadioStations.Count == 0 || IsRadioConnecting) return true;

        var currentIndex = RadioStations.IndexOf(CurrentRadioStation);
        RadioStationItemViewModel target;
        if (currentIndex < 0)
        {
            target = SelectedRadioStation ?? RadioStations[0];
        }
        else
        {
            var targetIndex = (currentIndex + offset) % RadioStations.Count;
            if (targetIndex < 0)
                targetIndex += RadioStations.Count;
            target = RadioStations[targetIndex];
        }

        if (ReferenceEquals(target, CurrentRadioStation)) return true;

        SelectedRadioStation = target;
        _ = StartRadioStationAsync(target, registerClick: true);
        return true;
    }

    /// <summary>
    /// Claims playback for a non-radio source (a local file or an online
    /// track). The returned version must be compared with
    /// <see cref="IsCurrentPlaybackVersion"/> after the load completes, and the
    /// radio adoption is abandoned so a pending station can never take the
    /// engine back.
    /// </summary>
    private int BeginLocalPlaybackAttempt()
    {
        int version;
        lock (playbackSourceLoadSync)
        {
            version = Interlocked.Increment(ref playbackSourceVersion);
        }

        connectingRadioStation = null;
        IsRadioConnecting = false;
        CurrentRadioStation = null;
        connectingOnlineTrack = null;
        IsOnlineConnecting = false;
        CurrentOnlineTrack = null;
        RefreshRadioStationPlaybackStates();
        RefreshOnlineTrackPlaybackStates();
        return version;
    }

    private void ClearPlaybackAfterFailedRadioLoad()
    {
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
        RefreshRadioStationPlaybackStates();
    }

    private bool IsCurrentPlaybackVersion(int version) =>
        !disposed && version == Volatile.Read(ref playbackSourceVersion);

    private void RefreshRadioStationPlaybackStates()
    {
        foreach (var station in RadioStations)
        {
            station.SetPlaybackState(
                current: ReferenceEquals(station, CurrentRadioStation),
                playing: IsPlaying,
                connecting: ReferenceEquals(station, connectingRadioStation));
        }

        if (CurrentRadioStation is not null &&
            !RadioStations.Contains(CurrentRadioStation))
        {
            CurrentRadioStation.SetPlaybackState(
                current: true,
                playing: IsPlaying,
                connecting: ReferenceEquals(
                    CurrentRadioStation,
                    connectingRadioStation));
        }

        if (connectingRadioStation is not null &&
            !RadioStations.Contains(connectingRadioStation) &&
            !ReferenceEquals(connectingRadioStation, CurrentRadioStation))
        {
            connectingRadioStation.SetPlaybackState(
                current: false,
                playing: false,
                connecting: true);
        }
    }

    private string BuildRadioNowPlayingSubtitle(RadioStationItemViewModel station)
    {
        var metadata = station.MetadataLabel;
        return metadata.Length == 0
            ? Loc["Radio_Title"]
            : $"{Loc["Radio_Title"]} · {metadata}";
    }

    private void SetRadioCatalogStatus(
        RadioCatalogStatus status,
        int stationCount,
        bool wasSearch)
    {
        radioCatalogStatus = status;
        radioCatalogStationCount = Math.Max(0, stationCount);
        radioCatalogWasSearch = wasSearch;
        RefreshRadioStatusLabel();
    }

    private void RefreshRadioStatusLabel()
    {
        RadioStatusLabel = radioCatalogStatus switch
        {
            RadioCatalogStatus.Loading => Loc["Radio_LoadingStations"],
            RadioCatalogStatus.Ready => Loc.Format(
                "Radio_StationsAvailable",
                radioCatalogStationCount),
            RadioCatalogStatus.Empty => Loc["Radio_NoStations"],
            RadioCatalogStatus.SearchEmpty => Loc["Radio_NoSearchResults"],
            RadioCatalogStatus.Failed => Loc["Radio_LoadFailed"],
            RadioCatalogStatus.Unavailable => Loc["Radio_Unavailable"],
            _ => Loc["Radio_Description"]
        };
    }

    private void RefreshRadioLocalizedLabels()
    {
        RefreshRadioStatusLabel();
        if (CurrentRadioStation is null) return;

        NowPlayingTitle = CurrentRadioStation.Name;
        NowPlayingSubtitle = BuildRadioNowPlayingSubtitle(CurrentRadioStation);
        DurationLabel = Loc["Radio_Live"];
    }

    private void DisableInternetRadio()
    {
        CancelRadioWork();
        IsRadioOpen = false;
        connectingRadioStation = null;
        IsRadioConnecting = false;
        if (IsRadioLoading)
        {
            IsRadioLoading = false;
            SetRadioCatalogStatus(
                RadioStations.Count == 0
                    ? RadioCatalogStatus.NotLoaded
                    : RadioCatalogStatus.Ready,
                RadioStations.Count,
                radioCatalogWasSearch);
        }

        if (CurrentRadioStation is null)
        {
            RefreshRadioStationPlaybackStates();
            return;
        }

        try
        {
            audioPlayer.Stop();
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not stop Internet Radio for {StationUuid}: {ExceptionType}",
                CurrentRadioStation.StationUuid,
                ex.GetType().FullName);
        }

        ClearPlaybackAfterFailedRadioLoad();
        RefreshPlaybackState();
    }

    private void CancelRadioWork()
    {
        Interlocked.Increment(ref playbackSourceVersion);
        radioSearchVersion++;
        radioSearchCancellation?.Cancel();
    }
}
