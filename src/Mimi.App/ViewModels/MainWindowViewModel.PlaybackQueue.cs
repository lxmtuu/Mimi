using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Behaviors;
using Mimi.Core.Playback;
using Mimi.Models;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Playback modes and the explicit playback queue. The Core queue owns the
/// ordering rules; this partial translates between queue occurrences,
/// library rows, audio loading, settings, and the Up Next presentation.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private readonly PlaybackQueue<PlaybackQueueItemViewModel> playbackQueue = new(
        cycleItemFactory: entry => new PlaybackQueueItemViewModel(entry.Track),
        representsSameItem: (left, right) => ReferenceEquals(left.Track, right.Track));
    private readonly Dictionary<TrackItemViewModel, PlaybackQueueItemViewModel>
        queueEntriesByTrack = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveVolumePercentLabel))]
    [NotifyPropertyChangedFor(nameof(MuteButtonLabel))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShuffleButtonLabel))]
    [NotifyPropertyChangedFor(nameof(QueueStatusLabel))]
    private bool isShuffleEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatModeLabel))]
    [NotifyPropertyChangedFor(nameof(IsRepeatEnabled))]
    [NotifyPropertyChangedFor(nameof(IsRepeatOne))]
    [NotifyPropertyChangedFor(nameof(QueueStatusLabel))]
    private RepeatMode repeatMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayQueueItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveQueueItemCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedQueueItemUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedQueueItemDownCommand))]
    private PlaybackQueueItemViewModel? selectedQueueItem;

    [ObservableProperty]
    private string audioOutputLabel = string.Empty;

    private AudioOutputStatus? lastAudioOutputStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudioOutputNotice))]
    private string audioOutputNotice = string.Empty;

    public ObservableCollection<PlaybackQueueItemViewModel> UpcomingQueueItems { get; } = [];

    public bool HasAudioOutputNotice =>
        !string.IsNullOrWhiteSpace(AudioOutputNotice);

    public string EffectiveVolumePercentLabel => IsMuted
        ? Loc["Playback_Muted"]
        : VolumePercentLabel;

    public string MuteButtonLabel => IsMuted
        ? Loc["Playback_Unmute"]
        : Loc["Playback_Mute"];

    public string ShuffleButtonLabel => IsShuffleEnabled
        ? Loc["Playback_ShuffleOn"]
        : Loc["Playback_ShuffleOff"];

    public string RepeatModeLabel => RepeatMode switch
    {
        RepeatMode.All => Loc["Playback_RepeatAll"],
        RepeatMode.One => Loc["Playback_RepeatOne"],
        _ => Loc["Playback_RepeatOff"]
    };

    public bool IsRepeatEnabled => RepeatMode != RepeatMode.Off;

    public bool IsRepeatOne => RepeatMode == RepeatMode.One;

    public bool HasUpcomingQueueItems => UpcomingQueueItems.Count > 0;

    public string QueueCountLabel => UpcomingQueueItems.Count == 1
        ? Loc["Queue_CountSingular"]
        : Loc.Format("Queue_CountPlural", UpcomingQueueItems.Count);

    public string QueueStatusLabel
    {
        get
        {
            var shuffle = IsShuffleEnabled
                ? Loc["Playback_ShuffleOnStatus"]
                : Loc["Playback_ShuffleOffStatus"];
            return Loc.Format(
                "Queue_Status",
                QueueCountLabel,
                shuffle,
                RepeatModeLabel);
        }
    }

    public Visibility TaskbarPlayButtonVisibility => IsPlaying
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility TaskbarPauseButtonVisibility => IsPlaying
        ? Visibility.Visible
        : Visibility.Collapsed;

    public double TaskbarProgressValue =>
        Math.Clamp(ProgressPercent / 100d, 0, 1);

    public TaskbarItemProgressState TaskbarProgressState => !HasCurrentPlaybackSource
        ? TaskbarItemProgressState.None
        : IsPlaying
            ? TaskbarItemProgressState.Normal
            : TaskbarItemProgressState.Paused;

    public string TaskbarDescription => !HasCurrentPlaybackSource
        ? "Mimi"
        : $"{NowPlayingTitle} — {NowPlayingSubtitle}";

    partial void OnIsMutedChanged(bool value)
    {
        ApplyEffectiveVolume();
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnIsShuffleEnabledChanged(bool value)
    {
        playbackQueue.SetShuffleEnabled(value);
        RefreshUpcomingQueue();
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        playbackQueue.RepeatMode = value;
        if (settingsLoaded && !isRestoringSettings)
            SaveSettings();
    }

    partial void OnCurrentTrackChanged(TrackItemViewModel? value)
    {
        if (trackWithPlaybackIndicator is not null &&
            !ReferenceEquals(trackWithPlaybackIndicator, value))
        {
            trackWithPlaybackIndicator.SetCurrentPlaybackState(
                isCurrentTrack: false,
                isPlaying: false);
        }

        value?.SetCurrentPlaybackState(
            isCurrentTrack: true,
            isPlaying: IsPlaying);
        trackWithPlaybackIndicator = value;

        StartLyricsLoad(value);

        if (value is null)
            AudioOutputNotice = string.Empty;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        CurrentTrack?.SetCurrentPlaybackState(
            isCurrentTrack: true,
            isPlaying: value);
        RefreshRadioStationPlaybackStates();
    }

    private void AudioPlayer_OutputStatusChanged(
        object? sender,
        AudioOutputStatusChangedEventArgs e) =>
        uiDispatcher.BeginInvoke(new Action(() =>
        {
            RefreshAudioOutputStatus(e.Status, e.PlaybackInterrupted);
            RefreshPlaybackState();

            // Device loss can be followed immediately by an app exit. Save
            // the retained song and position as soon as the interruption is
            // known rather than relying only on the periodic session timer.
            if (e.PlaybackInterrupted && CurrentTrack is not null)
                SaveSettings();
        }));

    private void RefreshAudioOutputStatus(
        AudioOutputStatus status,
        bool playbackInterrupted = false)
    {
        lastAudioOutputStatus = status;
        var deviceName = GetLocalizedAudioDeviceName(status);
        AudioOutputLabel = status switch
        {
            { IsAvailable: false } => Loc["Audio_NoOutput"],
            { Kind: AudioOutputKind.WasapiShared } =>
                Loc.Format("Audio_WasapiShared", deviceName),
            { Kind: AudioOutputKind.WaveOutFallback } =>
                Loc.Format("Audio_WaveOutFallback", deviceName),
            { Kind: AudioOutputKind.WaveOutForced } =>
                Loc.Format("Audio_WaveOutDiagnostic", deviceName),
            { Kind: AudioOutputKind.None, DeviceName: "Windows default output" } =>
                Loc["Audio_WindowsDefaultOutput"],
            _ => Loc.Format("Audio_WindowsDefault", deviceName)
        };

        if (playbackInterrupted || !status.IsAvailable)
        {
            AudioOutputNotice = GetLocalizedAudioOutputMessage(status);
        }
        else if (CurrentTrack is null)
        {
            AudioOutputNotice = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(AudioOutputNotice) &&
                 !audioPlayer.IsPlaying)
        {
            AudioOutputNotice = Loc.Format(
                "Audio_OutputReady",
                deviceName);
        }
    }

    private string GetLocalizedAudioOutputMessage(AudioOutputStatus status) =>
        status.MessageKind switch
        {
            AudioOutputMessageKind.Ready => Loc["Audio_MessageReady"],
            AudioOutputMessageKind.NoOutputAvailable =>
                Loc["Audio_MessageNoOutputAvailable"],
            AudioOutputMessageKind.WasapiReady =>
                Loc["Audio_MessageWasapiReady"],
            AudioOutputMessageKind.WasapiForcedReady =>
                Loc["Audio_MessageWasapiForcedReady"],
            AudioOutputMessageKind.WaveOutFallbackReady =>
                Loc["Audio_MessageWaveOutFallbackReady"],
            AudioOutputMessageKind.WaveOutForcedReady =>
                Loc["Audio_MessageWaveOutForcedReady"],
            AudioOutputMessageKind.WasapiUnavailableFallbackDisabled =>
                Loc["Audio_MessageWasapiUnavailableFallbackDisabled"],
            AudioOutputMessageKind.NoOutputPressPlay =>
                Loc["Audio_MessageNoOutputPressPlay"],
            AudioOutputMessageKind.PlaybackInterruptedOutputChanged =>
                Loc.Format(
                    "Audio_MessagePlaybackInterruptedOutputChanged",
                    GetLocalizedAudioDeviceName(status)),
            AudioOutputMessageKind.PlaybackInterruptedOutputUnavailable =>
                Loc["Audio_MessagePlaybackInterruptedOutputUnavailable"],
            AudioOutputMessageKind.CheckedWhenPlaybackStarts =>
                Loc["Audio_MessageCheckedWhenPlaybackStarts"],
            AudioOutputMessageKind.WindowsDefaultAvailable =>
                Loc["Audio_MessageWindowsDefaultAvailable"],
            AudioOutputMessageKind.OutputUnavailable =>
                Loc["Audio_MessageOutputUnavailable"],
            _ => status.Message
        };

    private string GetLocalizedAudioDeviceName(AudioOutputStatus status) =>
        status.DeviceName switch
        {
            "Windows audio output" => Loc["Audio_WindowsAudioOutput"],
            "Windows default output" => Loc["Audio_WindowsDefaultOutput"],
            "No output device" => Loc["Audio_NoOutputDevice"],
            _ => status.DeviceName
        };

    private void ShowAudioOutputUnavailable(
        AudioOutputUnavailableException ex)
    {
        RefreshAudioOutputStatus(
            ex.Status,
            playbackInterrupted: HasCurrentPlaybackSource);
        RefreshPlaybackState();
        Log.Warning(
            ex,
            "Playback could not start because no usable audio output is available");
        dialogService.ShowWarning(
            Loc.Format(
                "Audio_CannotStartPlayback",
                GetLocalizedAudioOutputMessage(ex.Status)),
            Loc["Settings_AudioOutput"]);
    }

    private void ApplyPlaybackPreferenceSettings()
    {
        IsMuted = settings.IsMuted;
        IsShuffleEnabled = settings.ShuffleEnabled;
        RepeatMode = Enum.TryParse<RepeatMode>(
            settings.RepeatMode,
            ignoreCase: true,
            out var savedRepeatMode)
            ? savedRepeatMode
            : RepeatMode.Off;
        playbackQueue.RepeatMode = RepeatMode;
        if (playbackQueue.IsShuffleEnabled != IsShuffleEnabled)
            playbackQueue.SetShuffleEnabled(IsShuffleEnabled);

        ApplyEffectiveVolume();
        RefreshUpcomingQueue();
    }

    private void ApplyEffectiveVolume() =>
        audioPlayer.Volume = IsMuted ? 0 : (float)Math.Clamp(Volume, 0, 1);

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void ToggleShuffle() => IsShuffleEnabled = !IsShuffleEnabled;

    [RelayCommand]
    private void CycleRepeatMode() => RepeatMode = RepeatMode switch
    {
        RepeatMode.Off => RepeatMode.All,
        RepeatMode.All => RepeatMode.One,
        _ => RepeatMode.Off
    };

    [RelayCommand]
    private void SeekBackward10() => SeekRelative(TimeSpan.FromSeconds(-10));

    [RelayCommand]
    private void SeekForward10() => SeekRelative(TimeSpan.FromSeconds(10));

    [RelayCommand]
    private void SeekBackward5() => SeekRelative(TimeSpan.FromSeconds(-5));

    [RelayCommand]
    private void SeekForward5() => SeekRelative(TimeSpan.FromSeconds(5));

    [RelayCommand]
    private void IncreaseVolume() => ChangeVolumeBy(0.05);

    [RelayCommand]
    private void DecreaseVolume() => ChangeVolumeBy(-0.05);

    [RelayCommand]
    private void ResumePlayback()
    {
        if (CurrentTrack is null)
        {
            PlayPause();
            return;
        }

        if (!audioPlayer.IsPlaying)
        {
            try
            {
                audioPlayer.Play();
            }
            catch (AudioOutputUnavailableException ex)
            {
                // SMTC/headset resume requests should update the visible
                // output notice without unexpectedly raising a modal dialog
                // while another application has focus.
                RefreshAudioOutputStatus(
                    ex.Status,
                    playbackInterrupted: true);
                Log.Warning(
                    ex,
                    "Could not resume playback because the audio output is unavailable");
            }
        }

        RefreshPlaybackState();
        SaveSettings();
    }

    [RelayCommand]
    private void PausePlayback()
    {
        if (HasCurrentPlaybackSource && audioPlayer.IsPlaying)
            audioPlayer.Pause();

        RefreshPlaybackState();
        SaveSettings();
    }

    public void SeekToPosition(TimeSpan position)
    {
        if (CurrentTrack is null || audioPlayer.Duration <= TimeSpan.Zero)
            return;

        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > audioPlayer.Duration) position = audioPlayer.Duration;

        audioPlayer.Seek(position);
        RefreshPlaybackPosition();
        SaveSettings();
    }

    private void SeekRelative(TimeSpan offset) =>
        SeekToPosition(audioPlayer.CurrentTime + offset);

    private void ChangeVolumeBy(double offset)
    {
        if (IsMuted)
            IsMuted = false;

        Volume = Math.Clamp(Volume + offset, 0, 1);
    }

    private PlaybackQueueItemViewModel GetOrCreateQueueEntry(
        TrackItemViewModel track)
    {
        if (queueEntriesByTrack.TryGetValue(track, out var entry))
            return entry;

        entry = new PlaybackQueueItemViewModel(track);
        queueEntriesByTrack.Add(track, entry);
        return entry;
    }

    private void SetPlaybackContext(TrackItemViewModel currentTrack)
    {
        var visible = TracksView.Cast<TrackItemViewModel>().ToList();
        if (!visible.Contains(currentTrack))
            visible = [currentTrack];

        playbackQueue.SetContext(
            visible.Select(GetOrCreateQueueEntry),
            GetOrCreateQueueEntry(currentTrack),
            IsShuffleEnabled);
        RefreshUpcomingQueue();
    }

    private bool TryPlayTrackFromCurrentView(
        TrackItemViewModel track,
        bool showError) =>
        TryPlayTrack(
            track,
            showError,
            () => SetPlaybackContext(track));

    private void EnsurePlaybackQueueForCurrentTrack()
    {
        if (CurrentTrack is null) return;

        if (playbackQueue.Current is not null &&
            ReferenceEquals(playbackQueue.Current.Track, CurrentTrack))
        {
            return;
        }

        SetPlaybackContext(CurrentTrack);
    }

    private bool TryPlayNextFromQueue(bool showErrors)
    {
        if (disposed) return false;

        EnsurePlaybackQueueForCurrentTrack();
        var candidates = playbackQueue.GetNextCandidates();
        if (candidates.Count == 0)
        {
            if (CurrentTrack is null)
            {
                var fallback = SelectedTrack ??
                               TracksView.Cast<TrackItemViewModel>().FirstOrDefault();
                return fallback is not null &&
                       TryPlayTrackFromCurrentView(fallback, showError: true);
            }

            RefreshPlaybackState();
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (TryPlayTrack(
                    candidate.Track,
                    showError: false,
                    () =>
                    {
                        playbackQueue.CommitNext(candidate);
                        RefreshUpcomingQueue();
                    }))
            {
                return true;
            }
        }

        RefreshUpcomingQueue();
        RefreshPlaybackState();
        if (showErrors)
        {
            dialogService.ShowWarning(
                Loc["Playback_NoneInQueue"],
                Loc["Playback_ErrorTitle"]);
        }

        return false;
    }

    private bool TryPlayPreviousFromQueue()
    {
        if (disposed) return false;

        if (CurrentTrack is not null &&
            audioPlayer.CurrentTime > TimeSpan.FromSeconds(3))
        {
            audioPlayer.Seek(TimeSpan.Zero);
            RefreshPlaybackPosition();
            SaveSettings();
            return true;
        }

        EnsurePlaybackQueueForCurrentTrack();
        var candidates = playbackQueue.GetPreviousCandidates();
        if (candidates.Count == 0)
        {
            if (CurrentTrack is null)
            {
                var fallback = SelectedTrack ??
                               TracksView.Cast<TrackItemViewModel>().LastOrDefault();
                return fallback is not null &&
                       TryPlayTrackFromCurrentView(fallback, showError: true);
            }

            audioPlayer.Seek(TimeSpan.Zero);
            RefreshPlaybackPosition();
            SaveSettings();
            return true;
        }

        foreach (var candidate in candidates)
        {
            if (TryPlayTrack(
                    candidate.Track,
                    showError: false,
                    () =>
                    {
                        playbackQueue.CommitPrevious(candidate);
                        RefreshUpcomingQueue();
                    }))
            {
                return true;
            }
        }

        RefreshUpcomingQueue();
        RefreshPlaybackState();
        dialogService.ShowWarning(
            Loc["Playback_NonePreviousInQueue"],
            Loc["Playback_ErrorTitle"]);
        return false;
    }

    [RelayCommand]
    private void PlayNextSelected()
    {
        var tracks = GetSelectedTracksSnapshot();
        if (tracks.Length == 0) return;

        playbackQueue.AddNextRange(
            tracks.Select(track => new PlaybackQueueItemViewModel(track)));
        RefreshUpcomingQueue();
        SaveSettings();
    }

    [RelayCommand]
    private void AddSelectedToQueue()
    {
        var tracks = GetSelectedTracksSnapshot();
        if (tracks.Length == 0) return;

        playbackQueue.AddLastRange(
            tracks.Select(track => new PlaybackQueueItemViewModel(track)));
        RefreshUpcomingQueue();
        SaveSettings();
    }

    private bool CanPlayQueueItem(PlaybackQueueItemViewModel? item) =>
        item is not null;

    private bool CanRemoveQueueItem(PlaybackQueueItemViewModel? item) =>
        item is not null && UpcomingQueueItems.Contains(item);

    private bool CanMoveSelectedQueueItemUp() =>
        SelectedQueueItem is not null &&
        UpcomingQueueItems.IndexOf(SelectedQueueItem) > 0;

    private bool CanMoveSelectedQueueItemDown()
    {
        if (SelectedQueueItem is null) return false;

        var index = UpcomingQueueItems.IndexOf(SelectedQueueItem);
        return index >= 0 && index + 1 < UpcomingQueueItems.Count;
    }

    private bool CanClearUpcomingQueue() => HasUpcomingQueueItems;

    [RelayCommand(CanExecute = nameof(CanPlayQueueItem))]
    private void PlayQueueItem(PlaybackQueueItemViewModel? item)
    {
        if (item is null) return;

        if (ReferenceEquals(item, playbackQueue.Current))
        {
            ResumePlayback();
            return;
        }

        TryPlayTrack(
            item.Track,
            showError: true,
            () =>
            {
                playbackQueue.JumpTo(item);
                RefreshUpcomingQueue();
            });
    }

    [RelayCommand(CanExecute = nameof(CanRemoveQueueItem))]
    private void RemoveQueueItem(PlaybackQueueItemViewModel? item)
    {
        if (item is null || !playbackQueue.RemoveUpcoming(item)) return;

        RefreshUpcomingQueue();
        SaveSettings();
    }

    [RelayCommand]
    private void MoveQueueItem(PlaylistMoveRequest? request)
    {
        if (request?.Item is not PlaybackQueueItemViewModel item) return;

        if (playbackQueue.MoveUpcoming(item, request.TargetPosition))
        {
            RefreshUpcomingQueue();
            SaveSettings();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedQueueItemUp))]
    private void MoveSelectedQueueItemUp()
    {
        if (SelectedQueueItem is null) return;

        var index = UpcomingQueueItems.IndexOf(SelectedQueueItem);
        if (index > 0 &&
            playbackQueue.MoveUpcoming(SelectedQueueItem, index - 1))
        {
            RefreshUpcomingQueue();
            SaveSettings();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedQueueItemDown))]
    private void MoveSelectedQueueItemDown()
    {
        if (SelectedQueueItem is null) return;

        var index = UpcomingQueueItems.IndexOf(SelectedQueueItem);
        if (index >= 0 && index + 1 < UpcomingQueueItems.Count &&
            playbackQueue.MoveUpcoming(SelectedQueueItem, index + 1))
        {
            RefreshUpcomingQueue();
            SaveSettings();
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearUpcomingQueue))]
    private void ClearUpcomingQueue()
    {
        playbackQueue.ClearUpcoming();
        RefreshUpcomingQueue();
        SaveSettings();
    }

    private void RefreshUpcomingQueue()
    {
        var selected = SelectedQueueItem;
        UpcomingQueueItems.Clear();

        for (var i = 0; i < playbackQueue.Upcoming.Count; i++)
        {
            var item = playbackQueue.Upcoming[i];
            item.IsCurrent = false;
            item.QueuePosition = i + 1;
            UpcomingQueueItems.Add(item);
        }

        if (playbackQueue.Current is not null)
        {
            playbackQueue.Current.IsCurrent = true;
            playbackQueue.Current.QueuePosition = 0;
        }

        SelectedQueueItem = selected is not null &&
                            UpcomingQueueItems.Contains(selected)
            ? selected
            : UpcomingQueueItems.FirstOrDefault();

        OnPropertyChanged(nameof(HasUpcomingQueueItems));
        OnPropertyChanged(nameof(QueueCountLabel));
        OnPropertyChanged(nameof(QueueStatusLabel));
        PlayQueueItemCommand.NotifyCanExecuteChanged();
        RemoveQueueItemCommand.NotifyCanExecuteChanged();
        MoveSelectedQueueItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedQueueItemDownCommand.NotifyCanExecuteChanged();
        ClearUpcomingQueueCommand.NotifyCanExecuteChanged();
    }

    private (List<string> SourcePaths, List<string> HistoryPaths, List<string> UpcomingPaths)
        GetPlaybackQueueSnapshot()
    {
        if (playbackQueue.SourceItems.Count == 0 && CurrentTrack is not null)
        {
            return ([CurrentTrack.FilePath], [], []);
        }

        return (
            playbackQueue.SourceItems.Select(item => item.Track.FilePath).ToList(),
            playbackQueue.History.Select(item => item.Track.FilePath).ToList(),
            playbackQueue.Upcoming.Select(item => item.Track.FilePath).ToList());
    }

    private void RestorePlaybackQueue(
        PlaybackSessionSettings session,
        TrackItemViewModel currentTrack)
    {
        var tracksByPath = Tracks
            .GroupBy(track => track.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var source = ResolveQueueContext(session.QueueTrackPaths, tracksByPath);
        var currentEntry = GetOrCreateQueueEntry(currentTrack);
        if (source.Count == 0 || !source.Contains(currentEntry))
        {
            SetPlaybackContext(currentTrack);
            return;
        }

        playbackQueue.RestoreSnapshot(
            source,
            ResolveQueueOccurrences(
                session.HistoryTrackPaths,
                tracksByPath,
                currentEntry),
            currentEntry,
            ResolveQueueOccurrences(
                session.UpcomingTrackPaths,
                tracksByPath,
                currentEntry),
            IsShuffleEnabled);
        RefreshUpcomingQueue();
    }

    private List<PlaybackQueueItemViewModel> ResolveQueueContext(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, TrackItemViewModel> tracksByPath)
    {
        var entries = new List<PlaybackQueueItemViewModel>();
        foreach (var path in paths)
        {
            if (!tracksByPath.TryGetValue(path, out var track)) continue;

            var entry = GetOrCreateQueueEntry(track);
            if (!entries.Contains(entry))
                entries.Add(entry);
        }

        return entries;
    }

    private List<PlaybackQueueItemViewModel> ResolveQueueOccurrences(
        IEnumerable<string> paths,
        IReadOnlyDictionary<string, TrackItemViewModel> tracksByPath,
        PlaybackQueueItemViewModel currentEntry)
    {
        var entries = new List<PlaybackQueueItemViewModel>();
        var usedEntries = new HashSet<PlaybackQueueItemViewModel>();

        foreach (var path in paths)
        {
            if (!tracksByPath.TryGetValue(path, out var track)) continue;

            var contextEntry = GetOrCreateQueueEntry(track);
            if (ReferenceEquals(contextEntry, currentEntry)) continue;

            var entry = usedEntries.Add(contextEntry)
                ? contextEntry
                : new PlaybackQueueItemViewModel(track);
            if (!ReferenceEquals(entry, contextEntry))
                usedEntries.Add(entry);

            entries.Add(entry);
        }

        return entries;
    }

    private void RemoveTrackFromPlaybackQueue(TrackItemViewModel track)
    {
        playbackQueue.RemoveAll(item => ReferenceEquals(item.Track, track));
        queueEntriesByTrack.Remove(track);
        RefreshUpcomingQueue();
    }
}
